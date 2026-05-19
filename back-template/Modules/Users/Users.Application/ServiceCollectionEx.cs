using Microsoft.Extensions.DependencyInjection;

namespace Users.Application;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddUsersApplicationServices(this IServiceCollection services)
    {
        return services;
    }
}
