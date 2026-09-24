using Authentication.Domain.Entities;

namespace Authentication.Domain.Repositories;

/// <summary>
/// Credenciales. Dos familias de metodos, y la diferencia es de seguridad:
/// <list type="bullet">
///   <item><c>...ForSignIn</c>: la autenticacion, que corre ANTES de conocer el tenant. Cruzan tenants y solo
///   buscan por claves unicas en todo el sistema (correo, id), asi que no pueden traer la fila equivocada.</item>
///   <item><c>...InTenant</c>: todo lo demas. Aplican el filtro de tenant de la peticion.</item>
/// </list>
/// Todas devuelven entidades RASTREADAS: los cambios se guardan con <c>IUnitOfWork.SaveChangesAsync</c>.
/// </summary>
public interface IUserCredentialRepository
{
    Task<UserCredential?> FindForSignInAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<UserCredential?> FindForSignInAsync(long credentialId, CancellationToken cancellationToken = default);

    /// <summary>Incluye las borradas: el indice unico tambien las cuenta.</summary>
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>Si el tenant ya tiene alguna credencial (el bootstrap solo corre sobre un tenant vacio).</summary>
    Task<bool> AnyInTenantAsync(long tenantId, CancellationToken cancellationToken = default);

    Task<UserCredential?> FindInTenantAsync(Guid publicId, CancellationToken cancellationToken = default);

    void Add(UserCredential credential);
}

public interface IRefreshTokenRepository
{
    /// <summary>Cruza tenants: el refresh llega sin contexto. Busca por hash, unico en todo el sistema.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(RefreshToken token);

    /// <summary>Revoca (sin guardar) todas las sesiones activas de una credencial. Devuelve cuantas.</summary>
    Task<int> RevokeAllActiveAsync(long credentialId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default);
}

public interface IPasswordSetupTokenRepository
{
    /// <summary>Cruza tenants: el enlace del correo llega sin sesion. Busca por hash.</summary>
    Task<PasswordSetupToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Consume (sin guardar) los tokens vivos de la credencial: solo uno vigente a la vez.</summary>
    Task InvalidateActiveAsync(long credentialId, DateTime nowUtc, CancellationToken cancellationToken = default);

    void Add(PasswordSetupToken token);
}

/// <summary>Roles y permisos. El tenant va EXPLICITO: la autenticacion los consulta antes de tener contexto.</summary>
public interface IRbacRepository
{
    /// <summary>Alta (idempotente) del catalogo global de permisos a partir de <c>RbacCatalog</c>.</summary>
    Task SyncPermissionCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Roles de sistema y sus concesiones en el tenant. Aditivo e idempotente.</summary>
    Task EnsureTenantProvisionedAsync(long tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<long>> GetProvisionedTenantIdsAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> GetGrantsAsync(
        long tenantId, long credentialId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetRolesByCodesAsync(long tenantId, IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Role Role, IReadOnlyList<string> Permissions)>> ListRolesAsync(long tenantId, CancellationToken cancellationToken = default);

    void AssignRole(long tenantId, long credentialId, long roleId);
}
