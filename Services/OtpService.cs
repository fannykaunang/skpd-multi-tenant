using System.Security.Cryptography;
using MySqlConnector;

namespace skpd_multi_tenant_api.Services;

public interface IOtpService
{
    Task<string> GenerateAndStoreAsync(
        MySqlConnection connection,
        string email,
        string type = "login",
        CancellationToken ct = default);

    Task<bool> VerifyAsync(
        MySqlConnection connection,
        string email,
        string code,
        string type = "login",
        CancellationToken ct = default);
}

public sealed class OtpService : IOtpService
{
    private const int OtpExpiryMinutes = 5;

    public async Task<string> GenerateAndStoreAsync(
        MySqlConnection connection,
        string email,
        string type = "login",
        CancellationToken ct = default)
    {
        // Invalidate previous unused OTPs for this email+type
        await using (var invalidateCmd = connection.CreateCommand())
        {
            invalidateCmd.CommandText = @"
                UPDATE otp_codes
                SET is_used = 1
                WHERE email = @email
                  AND type = @type
                  AND is_used = 0";
            invalidateCmd.Parameters.AddWithValue("@email", email);
            invalidateCmd.Parameters.AddWithValue("@type", type);
            await invalidateCmd.ExecuteNonQueryAsync(ct);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO otp_codes (email, code, type, expires_at, is_used, created_at)
            VALUES (@email, @code, @type, @expiresAt, 0, UTC_TIMESTAMP())";
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@expiresAt", DateTime.UtcNow.AddMinutes(OtpExpiryMinutes));

        await cmd.ExecuteNonQueryAsync(ct);

        return code;
    }

    public async Task<bool> VerifyAsync(
        MySqlConnection connection,
        string email,
        string code,
        string type = "login",
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id FROM otp_codes
            WHERE email = @email
              AND code = @code
              AND type = @type
              AND is_used = 0
              AND expires_at > UTC_TIMESTAMP()
            LIMIT 1";
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@type", type);

        var otpId = await cmd.ExecuteScalarAsync(ct);

        if (otpId is null)
            return false;

        // Mark as used
        await using var markCmd = connection.CreateCommand();
        markCmd.CommandText = "UPDATE otp_codes SET is_used = 1 WHERE id = @id";
        markCmd.Parameters.AddWithValue("@id", otpId);
        await markCmd.ExecuteNonQueryAsync(ct);

        return true;
    }
}
