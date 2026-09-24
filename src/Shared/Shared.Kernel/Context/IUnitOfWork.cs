namespace Shared.Kernel.Context;

/// <summary>
/// Una transaccion alrededor de varios pasos de escritura, aunque crucen repositorios o modulos (todos
/// comparten el mismo DbContext por peticion). Ejemplos: rotar un refresh token (revocar el viejo y crear el
/// nuevo), dar de alta un usuario con sus roles y su perfil. O todo o nada.
///
/// Anidable: si ya hay una transaccion abierta, el trabajo corre dentro de ella.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);

    /// <summary>Guarda todo lo rastreado en el contexto de la peticion (de cualquier repositorio).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Fija el tenant para un trabajo que NO viene de una peticion de ese tenant: el bootstrap (anonimo), una
/// tarea programada que recorre tenants. Actualiza el contexto que leen el filtro de EF y el GUC de RLS, y lo
/// restaura al terminar.
///
/// <para>Es la unica forma correcta de que un proceso de sistema escriba en una tabla con RLS: sin tenant en
/// contexto la policy rechaza la fila. Recorrer tenants con <c>IgnoreQueryFilters</c> NO funciona: cruza la
/// barrera de EF pero no la de RLS, y devuelve cero filas en silencio.</para>
/// </summary>
public interface ITenantScope
{
    IDisposable Enter(long tenantId);
}
