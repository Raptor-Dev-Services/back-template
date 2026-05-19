using Microsoft.Extensions.DependencyInjection;
using Tenancy.Domain.Repositories;
using Tenancy.Infrastructure.Persistence.SQLDB;
using Tenancy.Infrastructure.Repositories;

namespace Tenancy.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<TenantsSql>();
        services.AddScoped<BranchesSql>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        return services;
    }
}
