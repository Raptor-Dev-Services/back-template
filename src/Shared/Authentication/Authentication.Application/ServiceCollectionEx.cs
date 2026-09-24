using Microsoft.Extensions.DependencyInjection;

namespace Authentication.Application;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddAuthenticationApplicationServices(this IServiceCollection services)
    {
        return services;
    }
}
