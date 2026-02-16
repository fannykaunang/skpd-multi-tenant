namespace skpd_multi_tenant.Models;

public sealed class LoginResult
{
    public bool IsSuccess { get; init; }
    public bool IsLocked { get; init; }
    public LoginResponse? Response { get; init; }

    public static LoginResult Success(LoginResponse response)
        => new() { IsSuccess = true, Response = response };

    public static LoginResult Invalid()
        => new();

    public static LoginResult Locked()
        => new() { IsLocked = true };
}
