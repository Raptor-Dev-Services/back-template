using Shared.Kernel.Results;

namespace Shared.Kernel.BackgroundJobs;

/// <summary>
/// Contrato de una tarea programada (skill background-jobs). El despachador UNICO del sistema la descubre por su
/// <see cref="Code"/> y la corre cuando le toca; agregar una tarea es una clase mas que implementa esto y se
/// registra en DI en el modulo dueno de su logica. El despachador no se toca.
/// </summary>
public interface IAutomatedTask
{
    /// <summary>Identificador ESTABLE: es la clave de su fila de configuracion y de su historial.</summary>
    string Code { get; }

    /// <summary>
    /// Cada cuanto corre POR OMISION. Solo se usa al sembrar la fila la primera vez; despues el intervalo vive en
    /// la base y se cambia sin desplegar.
    /// </summary>
    int DefaultIntervalMinutes { get; }

    /// <summary>
    /// Un barrido completo. La tarea reporta que proceso y la ventana que cubrio. Debe ser IDEMPOTENTE: si la
    /// corrida sale parcial o falla, la ventana se reprocesa.
    /// </summary>
    Task<TaskRunResult> ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>SKIPPED y "no corrio" son cosas distintas: lo primero es una corrida exitosa sin nada que hacer.</summary>
public enum TaskRunStatus
{
    Success,
    Partial,
    Failed,
    Skipped,
}

public sealed record TaskRunResult(
    TaskRunStatus Status,
    int ItemsProcessed,
    int ItemsFailed,
    string? Message,
    DateTime WindowFromUtc,
    DateTime WindowToUtc)
{
    public static TaskRunResult Success(int processed, string? message, DateTime fromUtc, DateTime toUtc) =>
        new(TaskRunStatus.Success, processed, 0, message, fromUtc, toUtc);

    public static TaskRunResult Skipped(string message, DateTime fromUtc, DateTime toUtc) =>
        new(TaskRunStatus.Skipped, 0, 0, message, fromUtc, toUtc);

    public static TaskRunResult Partial(int processed, int failed, string message, DateTime fromUtc, DateTime toUtc) =>
        new(TaskRunStatus.Partial, processed, failed, message, fromUtc, toUtc);

    public static TaskRunResult Failed(string message, DateTime fromUtc, DateTime toUtc) =>
        new(TaskRunStatus.Failed, 0, 0, message, fromUtc, toUtc);
}

/// <summary>Una tarea en la lista de operacion: su configuracion y un resumen de su ultima corrida.</summary>
public sealed record AutomatedTaskStatusDto(
    string Code,
    bool IsEnabled,
    int IntervalMinutes,
    DateTime? LastRunAtUtc,
    DateTime NextRunAtUtc,
    DateTime? LastCutoffUtc,
    bool IsRunning,
    string? LastRunStatus,
    string? LastRunMessage);

public sealed record AutomatedTaskRunDto(
    long Id,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    string Status,
    int ItemsProcessed,
    int ItemsFailed,
    string? Message,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    Guid? TriggeredByUserId);

/// <summary>Consulta y configuracion del catalogo de tareas (puerto para los casos de uso de operacion).</summary>
public interface IAutomatedTaskStore
{
    Task<IReadOnlyList<AutomatedTaskStatusDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Pausar o reanudar sin desplegar. False si el codigo no existe.</summary>
    Task<bool> SetEnabledAsync(string code, bool isEnabled, CancellationToken cancellationToken = default);

    Task<PagedResult<AutomatedTaskRunDto>?> GetHistoryAsync(string code, int page, int pageSize, CancellationToken cancellationToken = default);
}

public enum RunNowOutcome
{
    Ran,
    AlreadyRunning,
    UnknownTask,
}

/// <summary>"Ejecutar ahora": misma logica que el despachador, disparada por una persona.</summary>
public interface IAutomatedTaskRunner
{
    Task<RunNowOutcome> RunNowAsync(string code, Guid? triggeredBy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuracion de las tareas (seccion <c>BackgroundJobs</c>).
///
/// <para><see cref="OperatorTenantId"/>: las tareas son de TODA la plataforma, no de un tenant. Operarlas exige el
/// permiso <c>tasks.manage</c> Y pertenecer al tenant operador. Sin configurarlo, los endpoints de operacion
/// responden 403 a todos: un administrador de un cliente no debe poder pausar tareas que afectan a los demas.</para>
/// </summary>
public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>Encender o apagar el despachador (las pruebas lo apagan).</summary>
    public bool DispatcherEnabled { get; set; } = true;

    /// <summary>Cada cuantos segundos mira el despachador si hay algo vencido. No es el intervalo de ninguna tarea.</summary>
    public int PollSeconds { get; set; } = 60;

    public long? OperatorTenantId { get; set; }
}
