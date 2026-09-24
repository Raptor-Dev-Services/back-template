namespace Authentication.Application.Dto;

/// <summary>Resultado de un inicio o renovacion de sesion. El refresh token en claro viaja SOLO aqui, una vez.</summary>
public sealed record AuthTokensDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

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
