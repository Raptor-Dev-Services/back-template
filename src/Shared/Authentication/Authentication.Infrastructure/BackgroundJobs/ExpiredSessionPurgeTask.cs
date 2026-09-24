using Authentication.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.BackgroundJobs;

namespace Authentication.Infrastructure.BackgroundJobs;

/// <summary>Configuracion de la purga (seccion <c>BackgroundJobs:SessionPurge</c>).</summary>
public sealed class SessionPurgeOptions
{
    public const string SectionName = "BackgroundJobs:SessionPurge";

    /// <summary>
    /// En seco POR OMISION: cuenta lo que purgaria y lo deja en el historial sin tocar nada. Una tarea nueva que
    /// borra se enciende a proposito, despues de ver en el historial que lo que cuenta es lo esperado.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>Dias que se conserva un token DESPUES de vencer (para investigar una sesion sospechosa).</summary>
    public int RetentionDays { get; set; } = 30;
}

/// <summary>
/// Tarea de ejemplo del patron: da de baja (soft delete, nunca DELETE) los refresh tokens y los tokens de
/// invitacion/restablecimiento vencidos hace mas de <see cref="SessionPurgeOptions.RetentionDays"/>. Recorre TODOS
/// los tenants (ignora solo el filtro de tenant; estas tablas estan fuera de RLS a proposito, ver 001_enable_rls.sql).
///
/// <para>Idempotente: una segunda corrida sobre la misma ventana no encuentra nada que marcar.</para>
/// </summary>
public sealed class ExpiredSessionPurgeTask(AppDbContext db, SessionPurgeOptions options) : IAutomatedTask
{
    public const string TaskCode = "auth.purge-expired-sessions";

    public string Code => TaskCode;
    public int DefaultIntervalMinutes => 24 * 60;

    public async Task<TaskRunResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.AddDays(-Math.Max(1, options.RetentionDays));

        var refreshTokens = db.Set<RefreshToken>().IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(t => t.ExpiresAtUtc < cutoffUtc);
        var setupTokens = db.Set<PasswordSetupToken>().IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(t => t.ExpiresAtUtc < cutoffUtc);

        if (options.DryRun)
        {
            var wouldPurge = await refreshTokens.CountAsync(cancellationToken) + await setupTokens.CountAsync(cancellationToken);
            return wouldPurge == 0
                ? TaskRunResult.Skipped("En seco: nada vencido que purgar.", DateTime.UnixEpoch, cutoffUtc)
                : TaskRunResult.Success(0, $"En seco: se purgarian {wouldPurge} tokens vencidos antes de {cutoffUtc:O}.", DateTime.UnixEpoch, cutoffUtc);
        }

        var purged = await refreshTokens.ExecuteUpdateAsync(s => s
            .SetProperty(t => t.IsDeleted, true)
            .SetProperty(t => t.DeletedAtUtc, nowUtc)
            .SetProperty(t => t.UpdatedAtUtc, nowUtc), cancellationToken);
        purged += await setupTokens.ExecuteUpdateAsync(s => s
            .SetProperty(t => t.IsDeleted, true)
            .SetProperty(t => t.DeletedAtUtc, nowUtc)
            .SetProperty(t => t.UpdatedAtUtc, nowUtc), cancellationToken);

        return purged == 0
            ? TaskRunResult.Skipped("Nada vencido que purgar.", DateTime.UnixEpoch, cutoffUtc)
            : TaskRunResult.Success(purged, $"Purgados {purged} tokens vencidos antes de {cutoffUtc:O}.", DateTime.UnixEpoch, cutoffUtc);
    }
}
