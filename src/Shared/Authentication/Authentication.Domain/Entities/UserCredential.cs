using Shared.Kernel.Domain;

namespace Authentication.Domain.Entities;

/// <summary>
/// Credencial de acceso de un usuario. El correo es unico en TODO el sistema: el login no pide tenant, lo
/// deduce de la credencial.
///
/// <para>Sin policy de RLS a proposito: el login la lee ANTES de que exista contexto de tenant. La protegen el
/// filtro global de EF en toda consulta que no sea de autenticacion, y que las de autenticacion buscan por
/// claves unicas en todo el sistema (correo, PublicId, id).</para>
/// </summary>
public sealed class UserCredential : TenantEntity
{
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>Normalizado (recortado y en minusculas) con <see cref="Identity.NormalizeEmail"/>.</summary>
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Desactivada (baja): no inicia sesion ni renueva. La apaga la baja del perfil en Users.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Bloqueada por un administrador: no inicia sesion ni renueva, y sus sesiones se revocan.</summary>
    public bool IsLocked { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }

    /// <summary>
    /// Secreto TOTP CIFRADO (AES-GCM, ver <c>ISecretProtector</c>): ni en claro ni hasheado, porque el servidor
    /// lo necesita para verificar cada codigo. Existe "pendiente" entre el setup y la activacion.
    /// </summary>
    public string? TotpSecretProtected { get; set; }

    /// <summary>Cuando se ACTIVO el 2FA. Null = inactivo (aunque haya un secreto pendiente).</summary>
    public DateTime? TotpEnabledAtUtc { get; set; }

    /// <summary>
    /// Ultimo paso TOTP (periodo de 30 s) consumido. Anti-replay: un codigo de un paso menor o igual ya se uso y
    /// se rechaza, asi el mismo codigo no sirve dos veces dentro de su ventana de validez.
    /// </summary>
    public long LastTotpStep { get; set; }

    public bool IsTwoFactorEnabled => TotpEnabledAtUtc is not null;

    /// <summary>Puede iniciar sesion o renovarla: activa, no bloqueada y no borrada.</summary>
    public bool CanSignIn => IsActive && !IsLocked && !IsDeleted;
}
