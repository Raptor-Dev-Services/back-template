using Common.Data;
using Domain.Repositories.Example;
using Infrastructure.Persistence.SQLDB.Main.Example;
using Infrastructure.PostgreSql;
using Infrastructure.Repositories.Example;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Extensiones de <see cref="IServiceCollection"/> para registrar los servicios de la capa Infrastructure.
/// </summary>
public static class ServiceCollectionEx
{
    /// <summary>
    /// Registra fábricas de conexión, clases SQL, repositorios y servicios externos.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <returns>La misma <paramref name="services"/> para encadenamiento.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<MainDbConnectionFactory>();
        services.AddSingleton<IOpenDbConnectionFactory>(sp => sp.GetRequiredService<MainDbConnectionFactory>());
        services.AddScoped<MainDapperDbConnection>();

        services.AddScoped<ExampleUsersSql>();

        services.AddScoped<IExampleUserRepository, ExampleUserRepository>();

        return services;
    }
}
