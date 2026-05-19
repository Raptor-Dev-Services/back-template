using Microsoft.Extensions.DependencyInjection;
using Tenancy.Application.Api;
using Tenancy.Contracts.Interfaces;

namespace Tenancy.Application;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITenancyApi, TenancyApi>();
        return services;
    }
}
