using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kernel.BackgroundJobs;

namespace Shared.Infrastructure.BackgroundJobs;

/// <summary>
/// "Reclamar, ejecutar, cerrar", compartido por el despachador y por "ejecutar ahora": las dos vias corren la misma
/// tarea y registran el resultado igual. Solo cambia quien dispara.
/// </summary>
public sealed class AutomatedTaskRunner(
    AutomatedTaskRepository repository,
    IEnumerable<IAutomatedTask> tasks,
    ILogger<AutomatedTaskRunner> logger) : IAutomatedTaskRunner
{
    private readonly IReadOnlyDictionary<string, IAutomatedTask> _byCode = tasks.ToDictionary(t => t.Code);

    public async Task RunDueTasksAsync(CancellationToken cancellationToken)
    {
        foreach (var code in await repository.ClaimAllDueAsync(DateTime.UtcNow, cancellationToken))
            await ExecuteClaimedAsync(code, triggeredBy: null, cancellationToken);
    }

    public async Task<RunNowOutcome> RunNowAsync(string code, Guid? triggeredBy, CancellationToken cancellationToken = default)
    {
        if (!_byCode.ContainsKey(code))
            return RunNowOutcome.UnknownTask;
        if (!await repository.ClaimByCodeAsync(code, DateTime.UtcNow, cancellationToken))
            return RunNowOutcome.AlreadyRunning;

        await ExecuteClaimedAsync(code, triggeredBy, cancellationToken);
        return RunNowOutcome.Ran;
    }

    private async Task ExecuteClaimedAsync(string code, Guid? triggeredBy, CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        if (!_byCode.TryGetValue(code, out var task))
        {
            logger.LogWarning("Se reclamo la tarea {Code} pero ninguna tarea registrada responde a ese codigo.", code);
            await repository.ReleaseWithoutRecordingAsync(code, cancellationToken);
            return;
        }

        TaskRunResult result;
        try
        {
            result = await task.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Fallo la tarea programada {Code}.", code);
            result = TaskRunResult.Failed("La corrida fallo; el detalle esta en el log.", startedAtUtc, DateTime.UtcNow);
        }

        await repository.RecordRunAsync(code, result, startedAtUtc, DateTime.UtcNow, triggeredBy, cancellationToken);
        logger.LogInformation("Tarea {Code}: {Status}, {Processed} procesados, {Failed} fallidos.",
            code, result.Status, result.ItemsProcessed, result.ItemsFailed);
    }
}

/// <summary>
/// El despachador UNICO: despierta cada <c>PollSeconds</c>, reclama lo vencido y lo corre. Un fallo de un ciclo no
/// lo mata: se registra y se reintenta en el siguiente.
/// </summary>
public sealed class AutomatedTaskDispatcherService(
    IServiceScopeFactory scopes, BackgroundJobsOptions options, ILogger<AutomatedTaskDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.DispatcherEnabled)
        {
            logger.LogInformation("Despachador de tareas programadas apagado (BackgroundJobs:DispatcherEnabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AutomatedTaskRunner>().RunDueTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // apagado normal
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo un ciclo del despachador de tareas; se reintenta en el siguiente.");
            }
        }
    }
}

/// <summary>Siembra (insert-only) la fila de cada tarea registrada al arrancar.</summary>
public sealed class AutomatedTaskCatalogSeedService(IServiceScopeFactory scopes, ILogger<AutomatedTaskCatalogSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<AutomatedTaskRepository>();
            foreach (var task in scope.ServiceProvider.GetServices<IAutomatedTask>())
                await repository.EnsureSeededAsync(task.Code, task.DefaultIntervalMinutes, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo sembrar el catalogo de tareas programadas; se reintenta en el proximo arranque.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
