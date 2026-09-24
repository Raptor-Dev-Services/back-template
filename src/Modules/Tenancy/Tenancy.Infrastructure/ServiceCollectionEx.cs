using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Tenancy;
using Tenancy.Domain.Repositories;
using Tenancy.Infrastructure.Persistence;
using Tenancy.Infrastructure.Repositories;
using Tenancy.Infrastructure.Services;

namespace Tenancy.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyInfrastructureServices(this IServiceCollection services)
    {
        services.AddModuleModel<TenantConfiguration>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITenantStatusProvider, TenantStatusProvider>();
        return services;
    }
}
