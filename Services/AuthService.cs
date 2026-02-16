using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using skpd_multi_tenant.Models;
using skpd_multi_tenant.Options;
using MySqlConnector;
using System.Data;

namespace skpd_multi_tenant.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(
        LoginRequest request,
        int? tenantSkpdId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default);
}


public sealed class AuthService(IMySqlConnectionFactory connectionFactory, IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    const string DummyHash = "$2a$11$abcdefghijklmnopqrstuv123456789012345678901234";

    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        int? tenantSkpdId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // 1️⃣ Rate limit per IP
        await using (var rateCmd = connection.CreateCommand())
        {
            rateCmd.CommandText = @"
            SELECT COUNT(*) FROM login_attempts_ip
            WHERE ip_address = @ip
              AND attempt_time > (UTC_TIMESTAMP() - INTERVAL 1 MINUTE)";
            rateCmd.Parameters.AddWithValue("@ip", ipAddress);

            var count = Convert.ToInt32(
                await rateCmd.ExecuteScalarAsync(cancellationToken));

            if (count >= 10)
                return LoginResult.Invalid(); // tetap generic
        }

        await using (var insertAttempt = connection.CreateCommand())
        {
            insertAttempt.CommandText =
                "INSERT INTO login_attempts_ip (ip_address, attempt_time) VALUES (@ip, UTC_TIMESTAMP())";
            insertAttempt.Parameters.AddWithValue("@ip", ipAddress);
            await insertAttempt.ExecuteNonQueryAsync(cancellationToken);
        }

        // 2️⃣ Fetch user
        await using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT id, skpd_id, username, email, password_hash,
               is_active, failed_login_attempt,
               lockout_until, last_failed_login, last_login
        FROM users
        WHERE (username = @identity OR email = @identity)
          AND deleted_at IS NULL
        LIMIT 1;";
        command.Parameters.AddWithValue("@identity", request.UsernameOrEmail);

        AuthUser? user = null;

        await using (var reader =
            await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                user = new AuthUser
                {
                    Id = reader.GetInt64("id"),
                    SkpdId = reader.IsDBNull("skpd_id")
                        ? null
                        : reader.GetInt32("skpd_id"),
                    Username = reader.GetString("username"),
                    Email = reader.GetString("email"),
                    PasswordHash = reader.GetString("password_hash"),
                    IsActive = reader.GetBoolean("is_active"),
                    FailedLoginAttempt = reader.GetInt32("failed_login_attempt"),
                    LockoutUntil = reader.IsDBNull("lockout_until")
                        ? null
                        : reader.GetDateTime("lockout_until")
                };
            }
        }

        // 3️⃣ Constant-time protection
        var hashToVerify = user?.PasswordHash ?? DummyHash;
        var passwordValid =
            BCrypt.Net.BCrypt.Verify(request.Password, hashToVerify);

        if (user == null)
        {
            if (user != null)
                await HandleFailedLogin(connection, user, cancellationToken);

            await InsertAuditLog(connection,
                user?.Id,
                tenantSkpdId,
                request.UsernameOrEmail,
                ipAddress,
                userAgent,
                "failed",
                "invalid_credentials",
                cancellationToken);

            return LoginResult.Invalid();
        }

        if (!passwordValid)
        {
            await HandleFailedLogin(connection, user, cancellationToken);
            await InsertAuditLog(connection,
                user.Id,
                user.SkpdId,
                request.UsernameOrEmail,
                ipAddress,
                userAgent,
                "failed",
                "invalid_password",
                cancellationToken);

            return LoginResult.Invalid();
        }

        if (!user.IsActive)
            return LoginResult.Invalid();

        if (user.LockoutUntil.HasValue &&
            user.LockoutUntil > DateTime.UtcNow)
        {
            await InsertAuditLog(connection,
                user.Id,
                user.SkpdId,
                request.UsernameOrEmail,
                ipAddress,
                userAgent,
                "locked",
                "account_locked",
                cancellationToken);

            return LoginResult.Locked();
        }

        if (tenantSkpdId.HasValue &&
            user.SkpdId != tenantSkpdId)
            return LoginResult.Invalid();

        // 4️⃣ Success
        await ResetFailedLogin(connection, user.Id, cancellationToken);

        await LoadUserRolesAsync(connection, user, cancellationToken);

        var response =
            await CreateTokensAndStoreRefreshTokenAsync(
                connection, user, cancellationToken);

        await InsertAuditLog(connection,
            user.Id,
            user.SkpdId,
            request.UsernameOrEmail,
            ipAddress,
            userAgent,
            "success",
            "authenticated",
            cancellationToken);

        return LoginResult.Success(response);
    }

    private async Task LoadUserRolesAsync(
        MySqlConnection connection,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        await using var roleCommand = connection.CreateCommand();
        roleCommand.CommandText = @"
        SELECT r.name
        FROM roles r
        JOIN user_roles ur ON ur.role_id = r.id
        WHERE ur.user_id = @userId
          AND r.skpd_id = @skpdId;";

        roleCommand.Parameters.AddWithValue("@userId", user.Id);
        roleCommand.Parameters.AddWithValue("@skpdId", user.SkpdId);

        await using var reader = await roleCommand.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            user.Roles.Add(reader.GetString("name"));
        }
    }

    private async Task<LoginResponse> CreateTokensAndStoreRefreshTokenAsync(
        MySqlConnection connection,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays);
        var refreshToken = GenerateRefreshToken();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            //new(JwtRegisteredClaimNames.Email, user.Email),
            new("skpd_id", user.SkpdId?.ToString() ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim("role", role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: accessTokenExpiry,
            signingCredentials: creds);

        await using var insertRefreshTokenCommand = connection.CreateCommand();
        insertRefreshTokenCommand.CommandText = @"INSERT INTO refresh_tokens (user_id, token, expires_at, revoked)
                                                 VALUES (@userId, @token, @expiresAt, 0)";
        insertRefreshTokenCommand.Parameters.AddWithValue("@userId", user.Id);
        insertRefreshTokenCommand.Parameters.AddWithValue("@token", refreshToken);
        insertRefreshTokenCommand.Parameters.AddWithValue("@expiresAt", refreshTokenExpiry);
        await insertRefreshTokenCommand.ExecuteNonQueryAsync(cancellationToken);

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
            ExpiresAtUtc = accessTokenExpiry,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiry,
            SkpdId = user.SkpdId,
            Username = user.Username
        };
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private async Task HandleFailedLogin(MySqlConnection connection, AuthUser user, CancellationToken ct)
    {
        var newCount = user.FailedLoginAttempt + 1;
        DateTime? lockout = null;

        if (newCount >= 5 && newCount < 10)
            lockout = DateTime.UtcNow.AddMinutes(15);
        else if (newCount >= 10 && newCount < 20)
            lockout = DateTime.UtcNow.AddHours(1);
        else if (newCount >= 20)
            lockout = DateTime.UtcNow.AddHours(24);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE users
        SET failed_login_attempt = @count,
            last_failed_login = UTC_TIMESTAMP(),
            lockout_until = @lockout
        WHERE id = @id";

        cmd.Parameters.AddWithValue("@count", newCount);
        cmd.Parameters.AddWithValue("@lockout", lockout);
        cmd.Parameters.AddWithValue("@id", user.Id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task ResetFailedLogin(MySqlConnection connection, long userId, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE users
        SET failed_login_attempt = 0,
            lockout_until = NULL,
            last_login = UTC_TIMESTAMP()
        WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", userId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task InsertAuditLog(
        MySqlConnection connection,
        long? userId,
        int? skpdId,
        string identity,
        string ip,
        string userAgent,
        string status,
        string reason,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO audit_logs
        (user_id, skpd_id, action, event_type, status, reason, identity,
         entity_type, entity_id, ip_address, user_agent, created_at)
        VALUES
        (@uid, @skpd, @action, @eventType, @status, @reason, @identity,
         NULL, NULL, @ip, @ua, UTC_TIMESTAMP())";

        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@skpd", skpdId);
        cmd.Parameters.AddWithValue("@action", "LOGIN_ATTEMPT");
        cmd.Parameters.AddWithValue("@eventType", "auth.login");
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@identity", identity);
        cmd.Parameters.AddWithValue("@ip", ip);
        cmd.Parameters.AddWithValue("@ua", userAgent);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
