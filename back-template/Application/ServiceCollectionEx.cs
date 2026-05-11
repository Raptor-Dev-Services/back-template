using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediator(Assembly.GetExecutingAssembly());

        return services;
    }
}
