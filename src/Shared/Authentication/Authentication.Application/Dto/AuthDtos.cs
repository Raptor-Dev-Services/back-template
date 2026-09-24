namespace Authentication.Application.Dto;

/// <summary>Resultado de un inicio o renovacion de sesion. El refresh token en claro viaja SOLO aqui, una vez.</summary>
public sealed record AuthTokensDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

/// <summary>
/// Resultado del login. Sin 2FA trae la sesion completa. Con 2FA activo NO trae tokens: trae
/// <see cref="TwoFactorRequired"/> = true y un <see cref="ChallengeToken"/> efimero que el cliente canjea en
/// <c>/auth/login/2fa</c> junto con el codigo.
/// </summary>
public sealed record LoginResultDto(
    string? AccessToken,
    DateTime? AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAtUtc,
    bool TwoFactorRequired = false,
    string? ChallengeToken = null)
{
    public static LoginResultDto From(AuthTokensDto tokens) =>
        new(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);

    public static LoginResultDto Challenge(string challengeToken) => new(null, null, null, null, true, challengeToken);
}

/// <summary>Primer paso del 2FA: el secreto (para teclearlo) y la URI otpauth (para pintarla como QR).</summary>
public sealed record TwoFactorSetupDto(string Secret, string OtpauthUri);

/// <summary>Codigos de recuperacion EN CLARO. Se muestran una sola vez: en la base solo queda su hash.</summary>
public sealed record RecoveryCodesDto(IReadOnlyList<string> Codes);

public sealed record BootstrapResultDto(Guid TenantPublicId, long TenantId, string TenantSlug, Guid AdminUserId, string AdminEmail);

public sealed record InvitedUserDto(Guid UserId, string Email, IReadOnlyList<string> Roles);

public sealed record RoleDto(string Code, string Name, bool IsSystem, IReadOnlyList<string> Permissions);

public sealed record AccountDto(
    Guid UserId,
    long TenantId,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTime? LastLoginAtUtc);

/// <summary>Acuse de una operacion que no devuelve datos (y que a proposito no confirma nada mas).</summary>
public sealed record AcceptedDto(bool Accepted = true);
