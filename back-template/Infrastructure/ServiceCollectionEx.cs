using Common.Data;
using Domain.Repositories.Example;
using Infrastructure.Persistence.SQLDB.Main.Example;
using Infrastructure.PostgreSql;
using Infrastructure.Repositories.Example;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database connection
        services.AddSingleton<MainDbConnectionFactory>();
        services.AddSingleton<IOpenDbConnectionFactory>(sp => sp.GetRequiredService<MainDbConnectionFactory>());
        services.AddScoped<MainDapperDbConnection>();

        // SQL objects
        services.AddScoped<ExampleUsersSql>();

        // Repositories
        services.AddScoped<IExampleUserRepository, ExampleUserRepository>();

        return services;
    }
}
