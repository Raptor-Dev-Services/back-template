using Microsoft.Extensions.DependencyInjection;

namespace Shared.Database;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddMainDatabase(this IServiceCollection services)
    {
        services.AddSingleton(typeof(DbConnectionFactory<>));
        services.AddScoped(typeof(DapperDbConnection<>));
        return services;
    }
}
