using Application.Services;
using Common.Data;
using Common.MultiTenancy;
using Domain.Repositories.Auth;
using Domain.Repositories.Users;
using Infrastructure.Persistence.SQLDB.Main.Auth;
using Infrastructure.Persistence.SQLDB.Main.Users;
using Infrastructure.PostgreSql;
using Infrastructure.Repositories.Auth;
using Infrastructure.Repositories.Users;
using Infrastructure.Services;
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
        // Conexión a BD
        services.AddSingleton<MainDbConnectionFactory>();
        services.AddSingleton<IOpenDbConnectionFactory>(sp => sp.GetRequiredService<MainDbConnectionFactory>());
        services.AddScoped<MainDapperDbConnection>();

        // Multi-tenancy context (Singleton — AsyncLocal por thread)
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // SQL objects
        services.AddScoped<UsersSql>();
        services.AddScoped<RefreshTokensSql>();

        // Repositorios
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Servicios de dominio
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        return services;
    }
}
