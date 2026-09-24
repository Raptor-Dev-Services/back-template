using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Tenancy.Presentation;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyWebApiServices(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
        return services;
    }
}
