using Shared.Kernel.Security;

namespace Authentication.Domain.Rbac;

/// <summary>Codigos de los roles de sistema.</summary>
public static class RoleCodes
{
    /// <summary>Administra el tenant: usuarios, roles, bitacora. El bootstrap crea al primero.</summary>
    public const string Admin = "Admin";

    /// <summary>Usuario operativo sin capacidades de administracion.</summary>
    public const string Member = "Member";
}

/// <summary>
/// La semilla RBAC: permisos, roles de sistema y que concede cada rol. Es la fuente de verdad del
/// aprovisionamiento de un tenant (bootstrap) y de la re-siembra al arrancar (un permiso nuevo llega solo a
/// los tenants que ya existian). La siembra es ADITIVA: nunca quita una concesion que un tenant haya hecho.
///
/// <para>Cada codigo es una constante de <see cref="KnownPermissions"/>: una prueba de arquitectura lo exige.</para>
/// </summary>
public static class RbacCatalog
{
    public static readonly IReadOnlyList<(string Code, string Name)> Permissions =
    [
        (KnownPermissions.UsersRead, "Ver usuarios"),
        (KnownPermissions.UsersManage, "Invitar, editar, bloquear y desactivar usuarios"),
        (KnownPermissions.AuditRead, "Consultar la bitacora de acciones"),
        (KnownPermissions.TasksManage, "Operar las tareas programadas"),
        (KnownPermissions.FilesRead, "Ver archivos del tenant"),
        (KnownPermissions.FilesWrite, "Subir archivos"),
    ];

    public static readonly IReadOnlyList<(string Code, string Name)> Roles =
    [
        (RoleCodes.Admin, "Administrador"),
        (RoleCodes.Member, "Miembro"),
    ];

    public static readonly IReadOnlyDictionary<string, string[]> RolePermissions = new Dictionary<string, string[]>
    {
        [RoleCodes.Admin] =
        [
            KnownPermissions.UsersRead, KnownPermissions.UsersManage, KnownPermissions.AuditRead,
            KnownPermissions.TasksManage, KnownPermissions.FilesRead, KnownPermissions.FilesWrite,
        ],
        [RoleCodes.Member] = [KnownPermissions.UsersRead, KnownPermissions.FilesRead, KnownPermissions.FilesWrite],
    };
}
