using Shared.Kernel.Domain;

namespace Authentication.Domain.Entities;

/// <summary>
/// Permiso RBAC. Catalogo GLOBAL (compartido por todos los tenants): el codigo es la clave natural y sale de
/// <c>KnownPermissions</c>. No se borra: un permiso que deja de existir en el codigo simplemente deja de
/// concederse.
/// </summary>
public sealed class Permission
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>Rol de un tenant. Los de sistema (<see cref="IsSystem"/>) los siembra el catalogo en cada tenant.</summary>
public sealed class Role : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

/// <summary>Concesion rol -> permiso dentro de un tenant.</summary>
public sealed class RolePermission : TenantEntity
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
}

/// <summary>Asignacion usuario -> rol dentro de un tenant.</summary>
public sealed class UserRole : TenantEntity
{
    public long CredentialId { get; set; }
    public long RoleId { get; set; }
}
