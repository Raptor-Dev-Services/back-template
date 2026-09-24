namespace Shared.Kernel.Security;

/// <summary>
/// El catalogo UNICO de permisos RBAC. Cada endpoint protegido declara el suyo con
/// <c>[Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.X)]</c>, el catalogo del modulo de
/// autenticacion los siembra en la base al arrancar, y el login los emite como claims <c>permission</c>.
///
/// <para>Dos pruebas de arquitectura lo custodian: que todo permiso que exige un endpoint exista aqui y se
/// siembre (si no, el endpoint responde 403 a TODO el mundo, en silencio), y que no haya una segunda lista de
/// codigos en otro lado.</para>
///
/// <para>Una accion irreversible no reusa el <c>manage</c> de su modulo: lleva permiso propio (regla
/// access-control).</para>
/// </summary>
public static class KnownPermissions
{
    /// <summary>Ver los usuarios del tenant.</summary>
    public const string UsersRead = "users.read";

    /// <summary>Invitar, editar, bloquear y desactivar usuarios del tenant.</summary>
    public const string UsersManage = "users.manage";

    /// <summary>Consultar la bitacora de acciones del tenant.</summary>
    public const string AuditRead = "audit.read";

    /// <summary>Ver, pausar y disparar a mano las tareas programadas (operacion de plataforma).</summary>
    public const string TasksManage = "tasks.manage";

    /// <summary>Pedir URLs de lectura de los archivos del tenant.</summary>
    public const string FilesRead = "files.read";

    /// <summary>Subir archivos a nombre del tenant.</summary>
    public const string FilesWrite = "files.write";
}
