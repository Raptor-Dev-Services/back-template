using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Context;

namespace Tenancy.Application.UseCases.AutomatedTasks;

/// <summary>
/// Las tareas programadas son de TODA la plataforma. El permiso <c>tasks.manage</c> (que la policy del endpoint ya
/// exigio) no basta: el Admin de cualquier cliente lo tiene. Ademas hay que pertenecer al tenant operador
/// (<c>BackgroundJobs:OperatorTenantId</c>). Sin configurarlo, nadie opera tareas desde la API.
/// </summary>
internal static class OperatorGate
{
    public const string DeniedMessage = "Solo el tenant operador de la plataforma puede operar las tareas programadas.";

    public static bool Allows(BackgroundJobsOptions options, ICurrentUser currentUser) =>
        options.OperatorTenantId is { } operatorTenant && currentUser.TenantId == operatorTenant;
}
