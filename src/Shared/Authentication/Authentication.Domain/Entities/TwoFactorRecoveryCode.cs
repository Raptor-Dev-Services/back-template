using Shared.Kernel.Domain;

namespace Authentication.Domain.Entities;

/// <summary>
/// Codigo de recuperacion de 2FA (para entrar si se pierde el telefono). Solo se guarda su hash; el codigo en
/// claro se muestra UNA vez, al activar el 2FA. Un solo uso.
///
/// Sin policy de RLS, como el resto de las tablas de autenticacion: se consulta en el segundo paso del login,
/// antes de que exista sesion. Su unica consulta re-acota por la credencial del reto.
/// </summary>
public sealed class TwoFactorRecoveryCode : TenantEntity
{
    public long CredentialId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime? ConsumedAtUtc { get; set; }

    public bool IsUsable => ConsumedAtUtc is null;

    public void Consume(DateTime nowUtc) => ConsumedAtUtc ??= nowUtc;
}
