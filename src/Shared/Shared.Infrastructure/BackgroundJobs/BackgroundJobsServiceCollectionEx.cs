using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.BackgroundJobs;

namespace Shared.Infrastructure.BackgroundJobs;

public static class BackgroundJobsServiceCollectionEx
{
    /// <summary>
    /// El despachador unico de tareas programadas, su siembra y los puertos de operacion. Las TAREAS se registran
    /// aparte, cada una en el modulo dueno de su logica (<c>services.AddScoped&lt;IAutomatedTask, MiTarea&gt;()</c>).
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, BackgroundJobsOptions options)
    {
        services.AddSingleton(options);

        services.AddScoped<AutomatedTaskRepository>();
        services.AddScoped<IAutomatedTaskStore>(provider => provider.GetRequiredService<AutomatedTaskRepository>());
        services.AddScoped<AutomatedTaskRunner>();
        services.AddScoped<IAutomatedTaskRunner>(provider => provider.GetRequiredService<AutomatedTaskRunner>());

        services.AddHostedService<AutomatedTaskCatalogSeedService>();
        services.AddHostedService<AutomatedTaskDispatcherService>();
        return services;
    }
}
