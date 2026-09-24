using Shared.Kernel.Domain;

namespace Authentication.Domain.Entities;

/// <summary>
/// Refresh token rotativo. Solo se guarda su HASH (SHA-256): quien lea la tabla no obtiene ninguna sesion.
///
/// <para>Rotar = revocar el actual (<see cref="RevokedAtUtc"/>) y encadenarlo al nuevo con
/// <see cref="ReplacedByTokenHash"/>. Presentar de nuevo un token ya rotado solo pasa si se filtro: es la senal
/// de reuso (RFC 6819) y revoca TODAS las sesiones del usuario.</para>
///
/// Sin policy de RLS: el refresh la lee por hash antes de conocer el tenant.
/// </summary>
public sealed class RefreshToken : TenantEntity
{
    public long CredentialId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Por que se revoco: rotated, logout, reuse, password_changed, locked, disabled.</summary>
    public string? RevokedReason { get; set; }

    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    public void Revoke(DateTime nowUtc, string reason, string? replacedByHash = null)
    {
        if (RevokedAtUtc is not null)
            return;
        RevokedAtUtc = nowUtc;
        RevokedReason = reason;
        ReplacedByTokenHash = replacedByHash;
    }
}

/// <summary>Motivos de revocacion de un refresh token (texto estable: queda en la base).</summary>
public static class RevocationReasons
{
    public const string Rotated = "rotated";
    public const string Logout = "logout";
    public const string Reuse = "reuse_detected";
    public const string PasswordChanged = "password_changed";
    public const string Locked = "locked";
    public const string Disabled = "disabled";
}
