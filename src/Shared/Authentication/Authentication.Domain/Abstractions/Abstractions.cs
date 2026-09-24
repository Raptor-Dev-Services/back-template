namespace Authentication.Domain.Abstractions;

/// <summary>Hash y verificacion de contrasenas. Nunca se guarda ni se registra la contrasena en claro.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifica la contrasena. Con <paramref name="passwordHash"/> null (el usuario no existe) compara igual
    /// contra un hash ficticio y devuelve false: responder mas rapido cuando el correo no existe le diria a un
    /// atacante que cuentas existen.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}

/// <summary>Quien es el titular del access token y que puede hacer.</summary>
public sealed record AccessTokenSubject(
    Guid UserId,
    long TenantId,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

/// <summary>Access token firmado y su expiracion (UTC).</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>
/// Emite el access JWT con los claims que lee el resto del sistema (<c>Shared.Kernel.Security.AppClaimTypes</c>):
/// sub, tenant_id, email, roles y permission. Lo firma con la MISMA clave que valida el Host.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AccessTokenSubject subject);
}

/// <summary>
/// Parametros de los tokens. Se enlazan de la seccion <c>Jwt</c> (clave, emisor, audiencia y vidas) y de
/// <c>Auth</c> (vidas de los enlaces por correo).
/// </summary>
public sealed class AuthOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Corto a proposito: la revocacion efectiva de una sesion llega, como tarde, al vencer el access token.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Vida del enlace de invitacion (fijar la primera contrasena).</summary>
    public int InvitationHours { get; set; } = 72;

    /// <summary>Vida del enlace de restablecimiento.</summary>
    public int PasswordResetMinutes { get; set; } = 60;
}
