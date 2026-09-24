using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Results;

namespace Shared.Infrastructure.BackgroundJobs;

/// <summary>
/// Persistencia de las tareas. El reclamo usa SQL PARAMETRIZADO (<c>FromSqlInterpolated</c>, nunca concatenado)
/// porque LINQ no expresa <c>FOR UPDATE SKIP LOCKED</c>, y es lo que hace que "reclamar" y "seleccionar" sean UNA
/// sentencia atomica: otra replica que despierte a la vez se salta lo que esta ya tomo.
/// </summary>
public sealed class AutomatedTaskRepository(AppDbContext db) : IAutomatedTaskStore
{
    /// <summary>
    /// Un reclamo solo se libera al terminar la corrida. Si el proceso muere antes (OOM, SIGKILL en un deploy) la
    /// marca quedaria para siempre y la tarea desapareceria del barrido SIN un error. Pasado este tiempo se da por
    /// huerfano. Holgado: mas que cualquier corrida real, menos que lo que tarda alguien en notar que algo dejo de pasar.
    /// </summary>
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromHours(6);

    /// <summary>Alta insert-only: una tarea que ya existe no se toca (su intervalo o su pausa viven en la base).</summary>
    public async Task EnsureSeededAsync(string code, int intervalMinutes, CancellationToken cancellationToken)
    {
        if (await db.Set<AutomatedTaskDefinition>().AnyAsync(t => t.Code == code, cancellationToken))
            return;

        db.Add(new AutomatedTaskDefinition
        {
            Code = code,
            IntervalMinutes = intervalMinutes,
            // No corre al arrancar: el primer turno natural es despues de un intervalo completo.
            NextRunAtUtc = DateTime.UtcNow.AddMinutes(intervalMinutes),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ClaimAllDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var staleBefore = nowUtc - ClaimTimeout;
        var claimed = await db.Set<AutomatedTaskDefinition>()
            .FromSqlInterpolated($"""
                UPDATE "AutomatedTaskDefinition" AS t
                SET "ClaimedAtUtc" = {nowUtc}
                FROM (
                    SELECT "Id" FROM "AutomatedTaskDefinition"
                    WHERE "IsEnabled" = TRUE
                      AND ("ClaimedAtUtc" IS NULL OR "ClaimedAtUtc" < {staleBefore})
                      AND "NextRunAtUtc" <= {nowUtc}
                    ORDER BY "NextRunAtUtc"
                    FOR UPDATE SKIP LOCKED
                ) AS due
                WHERE t."Id" = due."Id"
                RETURNING t.*
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. claimed.Select(t => t.Code)];
    }

    /// <summary>"Ejecutar ahora": ignora el horario y la pausa, pero respeta el reclamo (no corre dos veces a la vez).</summary>
    public async Task<bool> ClaimByCodeAsync(string code, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var staleBefore = nowUtc - ClaimTimeout;
        var claimed = await db.Set<AutomatedTaskDefinition>()
            .FromSqlInterpolated($"""
                UPDATE "AutomatedTaskDefinition"
                SET "ClaimedAtUtc" = {nowUtc}
                WHERE "Code" = {code} AND ("ClaimedAtUtc" IS NULL OR "ClaimedAtUtc" < {staleBefore})
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return claimed.Count > 0;
    }

    /// <summary>
    /// Cierra una corrida en UNA transaccion: historial, liberar el reclamo y reprogramar. El cursor
    /// (<c>LastCutoffUtc</c>) solo avanza si la corrida salio completa: con Partial o Failed, avanzarlo convertiria
    /// un fallo transitorio en trabajo perdido para siempre (la siguiente ventana empezaria donde termino esta).
    /// </summary>
    public async Task RecordRunAsync(string code, TaskRunResult result, DateTime startedAtUtc, DateTime finishedAtUtc,
        Guid? triggeredBy, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Add(new AutomatedTaskRun
        {
            TaskCode = code,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = finishedAtUtc,
            Status = result.Status.ToString().ToUpperInvariant(),
            ItemsProcessed = result.ItemsProcessed,
            ItemsFailed = result.ItemsFailed,
            Message = result.Message is { Length: > 2000 } m ? m[..2000] : result.Message,
            WindowFromUtc = result.WindowFromUtc,
            WindowToUtc = result.WindowToUtc,
            TriggeredByUserId = triggeredBy,
        });
        await db.SaveChangesAsync(cancellationToken);

        var interval = await db.Set<AutomatedTaskDefinition>().AsNoTracking()
            .Where(t => t.Code == code).Select(t => t.IntervalMinutes).FirstOrDefaultAsync(cancellationToken);
        var cursorAdvances = result.Status is TaskRunStatus.Success or TaskRunStatus.Skipped;

        // ExecuteUpdate y no cargar-y-mutar: el reclamo escribio con SQL crudo (fuera del change tracker) y una
        // instancia rastreada podria tener un ClaimedAtUtc viejo que SaveChanges no tocaria.
        await db.Set<AutomatedTaskDefinition>()
            .Where(t => t.Code == code)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.LastRunAtUtc, finishedAtUtc)
                .SetProperty(t => t.NextRunAtUtc, finishedAtUtc.AddMinutes(interval))
                .SetProperty(t => t.LastCutoffUtc, t => cursorAdvances ? result.WindowToUtc : t.LastCutoffUtc)
                .SetProperty(t => t.ClaimedAtUtc, (DateTime?)null), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Libera un reclamo sin historial (un Code en la base que ya no tiene tarea en el codigo).</summary>
    public Task ReleaseWithoutRecordingAsync(string code, CancellationToken cancellationToken) =>
        db.Set<AutomatedTaskDefinition>().Where(t => t.Code == code)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ClaimedAtUtc, (DateTime?)null), cancellationToken);

    public async Task<IReadOnlyList<AutomatedTaskStatusDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await db.Set<AutomatedTaskDefinition>().AsNoTracking().OrderBy(t => t.Code).ToListAsync(cancellationToken);
        var result = new List<AutomatedTaskStatusDto>(definitions.Count);
        foreach (var d in definitions)
        {
            var last = await db.Set<AutomatedTaskRun>().AsNoTracking()
                .Where(r => r.TaskCode == d.Code).OrderByDescending(r => r.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
            result.Add(new AutomatedTaskStatusDto(d.Code, d.IsEnabled, d.IntervalMinutes, d.LastRunAtUtc, d.NextRunAtUtc,
                d.LastCutoffUtc, d.ClaimedAtUtc is not null, last?.Status, last?.Message));
        }

        return result;
    }

    public async Task<bool> SetEnabledAsync(string code, bool isEnabled, CancellationToken cancellationToken = default) =>
        await db.Set<AutomatedTaskDefinition>().Where(t => t.Code == code)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsEnabled, isEnabled), cancellationToken) > 0;

    public async Task<PagedResult<AutomatedTaskRunDto>?> GetHistoryAsync(string code, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await db.Set<AutomatedTaskDefinition>().AnyAsync(t => t.Code == code, cancellationToken))
            return null;

        var (p, size) = Paging.Normalize(page, pageSize);
        var query = db.Set<AutomatedTaskRun>().AsNoTracking().Where(r => r.TaskCode == code);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.StartedAtUtc).ThenByDescending(r => r.Id)
            .Skip((p - 1) * size).Take(size)
            .Select(r => new AutomatedTaskRunDto(r.Id, r.StartedAtUtc, r.FinishedAtUtc, r.Status, r.ItemsProcessed,
                r.ItemsFailed, r.Message, r.WindowFromUtc, r.WindowToUtc, r.TriggeredByUserId))
            .ToListAsync(cancellationToken);

        return new PagedResult<AutomatedTaskRunDto>(items, p, size, total);
    }
}
