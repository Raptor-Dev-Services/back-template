using System.Globalization;
using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Storage;

namespace Shared.Infrastructure.Storage;

/// <summary>Configuracion de la purga de huerfanos (seccion <c>BackgroundJobs:OrphanObjects</c>).</summary>
public sealed class OrphanObjectPurgeOptions
{
    public const string SectionName = "BackgroundJobs:OrphanObjects";

    /// <summary>En seco POR OMISION: cuenta lo que borraria y lo deja en el historial sin tocar el bucket.</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Un objeto mas nuevo que esto NO se considera huerfano: la subida pone el objeto en el bucket ANTES de hacer
    /// commit de su fila, asi que un objeto recien subido y todavia sin fila es una subida en curso, no basura.
    /// </summary>
    public int GraceHours { get; set; } = 24;
}

/// <summary>
/// Concilia el bucket contra el registro de propiedad: borra los objetos que NO tienen fila en <c>StoredFile</c> (la
/// subida los dejo ahi y fallo al registrarlos, y el borrado de compensacion tambien fallo). Un objeto sin dueno no lo
/// puede leer nadie, pero ocupa espacio para siempre.
///
/// <para><b>Recorre tenant por tenant</b>, con el contexto del tenant fijado. <c>StoredFile</c> esta bajo RLS: una
/// consulta sin contexto devuelve CERO filas sin error, y eso haria que todos los objetos parecieran huerfanos. Por eso,
/// antes de borrar nada de un tenant, comprueba que la conexion de verdad lleva ese tenant; si no, se salta el tenant
/// y la corrida sale como fallo parcial.</para>
///
/// <para>Solo toca claves con el formato de <see cref="ObjectKeys.NewFor"/>. Una fila dada de baja (soft delete)
/// sigue contando como dueno: el objeto se conserva mientras exista su registro.</para>
/// </summary>
public sealed class OrphanObjectPurgeTask(
    IObjectStorage storage,
    IServiceScopeFactory scopes,
    ITenantContextAccessor tenantAccessor,
    OrphanObjectPurgeOptions options,
    ILogger<OrphanObjectPurgeTask> logger) : IAutomatedTask
{
    public const string TaskCode = "storage.purge-orphan-objects";

    public string Code => TaskCode;
    public int DefaultIntervalMinutes => 24 * 60;

    public async Task<TaskRunResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.AddHours(-Math.Max(1, options.GraceHours));

        var candidates = new Dictionary<long, List<string>>();
        var foreignKeys = 0;
        await foreach (var item in storage.ListAllAsync(cancellationToken))
        {
            if (item.LastModifiedUtc >= cutoffUtc)
                continue;
            if (!ObjectKeys.TryGetTenantId(item.ObjectKey, out var tenantId))
            {
                foreignKeys++;
                continue;
            }
            if (!candidates.TryGetValue(tenantId, out var keys))
                candidates[tenantId] = keys = [];
            keys.Add(item.ObjectKey);
        }

        if (foreignKeys > 0)
            logger.LogWarning("Purga de huerfanos: {Count} objetos sin el formato de clave de esta API; no se tocan.", foreignKeys);

        var orphans = 0;
        var deleted = 0;
        var failed = 0;
        var tenantsSkipped = 0;
        foreach (var (tenantId, keys) in candidates)
        {
            var owned = await OwnedKeysAsync(tenantId, keys, cancellationToken);
            if (owned is null)
            {
                tenantsSkipped++;
                continue;
            }

            foreach (var key in keys.Where(k => !owned.Contains(k)))
            {
                orphans++;
                if (options.DryRun)
                    continue;
                try
                {
                    await storage.DeleteAsync(key, cancellationToken);
                    deleted++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    logger.LogWarning(ex, "Purga de huerfanos: no se pudo borrar {ObjectKey}; se reintenta en la proxima corrida.", key);
                }
            }
        }

        if (tenantsSkipped > 0)
            return TaskRunResult.Partial(deleted, failed + tenantsSkipped,
                $"{tenantsSkipped} tenants sin contexto verificable: no se toco nada de ellos. Detalle en el log.", DateTime.UnixEpoch, cutoffUtc);
        if (options.DryRun)
            return orphans == 0
                ? TaskRunResult.Skipped("En seco: no hay objetos huerfanos.", DateTime.UnixEpoch, cutoffUtc)
                : TaskRunResult.Success(0, $"En seco: se borrarian {orphans} objetos huerfanos anteriores a {cutoffUtc:O}.", DateTime.UnixEpoch, cutoffUtc);
        if (failed > 0)
            return TaskRunResult.Partial(deleted, failed, $"Borrados {deleted} huerfanos; {failed} no se pudieron borrar.", DateTime.UnixEpoch, cutoffUtc);
        return deleted == 0
            ? TaskRunResult.Skipped("No hay objetos huerfanos.", DateTime.UnixEpoch, cutoffUtc)
            : TaskRunResult.Success(deleted, $"Borrados {deleted} objetos huerfanos anteriores a {cutoffUtc:O}.", DateTime.UnixEpoch, cutoffUtc);
    }

    /// <summary>
    /// Las claves de <paramref name="keys"/> que tienen fila en el registro del tenant (dadas de baja incluidas), o
    /// <c>null</c> si no se pudo comprobar que la conexion lleva ese tenant.
    /// </summary>
    private async Task<HashSet<string>?> OwnedKeysAsync(long tenantId, List<string> keys, CancellationToken cancellationToken)
    {
        var previous = tenantAccessor.Current;
        tenantAccessor.Current = new TenantContext(tenantId.ToString(CultureInfo.InvariantCulture));
        try
        {
            // Un scope nuevo por tenant: DbContext y conexion nuevos, asi que el interceptor de RLS fija ESTE tenant.
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var connectionTenant = await db.Database
                .SqlQueryRaw<string>("SELECT current_setting('app.tenant_id', true) AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (connectionTenant != tenantId.ToString(CultureInfo.InvariantCulture))
            {
                logger.LogError("Purga de huerfanos: la conexion lleva el tenant '{Actual}' y se esperaba {Expected}; se omite el tenant.",
                    connectionTenant, tenantId);
                return null;
            }

            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var batch in keys.Chunk(500))
            {
                var found = await db.Set<StoredFile>().AsNoTracking()
                    .IgnoreQueryFilters([QueryFilterNames.SoftDelete])
                    .Where(f => batch.Contains(f.ObjectKey))
                    .Select(f => f.ObjectKey)
                    .ToListAsync(cancellationToken);
                owned.UnionWith(found);
            }
            return owned;
        }
        finally
        {
            tenantAccessor.Current = previous;
        }
    }
}
