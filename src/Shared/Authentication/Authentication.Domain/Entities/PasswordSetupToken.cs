using Shared.Kernel.Domain;

namespace Authentication.Domain.Entities;

/// <summary>Para que se emitio un <see cref="PasswordSetupToken"/>.</summary>
public enum PasswordSetupPurpose
{
    /// <summary>Un administrador dio de alta al usuario: fija su primera contrasena.</summary>
    Invitation,

    /// <summary>El usuario pidio restablecerla.</summary>
    Reset,
}

/// <summary>
/// Token de un solo uso para fijar la contrasena (invitacion o restablecimiento). Solo se guarda su hash; el
/// valor en claro viaja UNICAMENTE en el enlace del correo, nunca en una respuesta de la API: devolverlo seria
/// una via de toma de cuenta.
///
/// Sin policy de RLS: se busca por hash desde un enlace anonimo, antes de conocer el tenant.
/// </summary>
public sealed class PasswordSetupToken : TenantEntity
{
    public long CredentialId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public PasswordSetupPurpose Purpose { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    public bool IsUsable(DateTime nowUtc) => ConsumedAtUtc is null && nowUtc < ExpiresAtUtc;

    /// <summary>Un solo uso. Idempotente: conserva el primer consumo.</summary>
    public void Consume(DateTime nowUtc) => ConsumedAtUtc ??= nowUtc;
}
