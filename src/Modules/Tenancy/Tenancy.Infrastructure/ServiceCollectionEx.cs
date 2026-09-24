using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;
using Tenancy.Domain.Repositories;
using Tenancy.Infrastructure.Persistence;
using Tenancy.Infrastructure.Repositories;

namespace Tenancy.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyInfrastructureServices(this IServiceCollection services)
    {
        services.AddModuleModel<TenantConfiguration>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        return services;
    }
}
