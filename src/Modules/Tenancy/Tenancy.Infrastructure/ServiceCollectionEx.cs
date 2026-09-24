using Microsoft.Extensions.DependencyInjection;
using Tenancy.Domain.Repositories;
using Tenancy.Infrastructure.Repositories;

namespace Tenancy.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<ITenantRepository, TenantRepository>();
        return services;
    }
}
