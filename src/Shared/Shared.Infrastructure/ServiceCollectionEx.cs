using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace Shared.Infrastructure;

public static class ServiceCollectionEx
{
    /// <summary>
    /// Registra el <see cref="AppDbContext"/> contra Postgres con la cadena de la APLICACION (rol sin DDL y
    /// sin BYPASSRLS), el interceptor que fija <c>app.tenant_id</c> en cada conexion y la guarda que tumba el
    /// arranque si ese rol puede saltarse RLS. Las migraciones corren aparte, con el rol dueno del esquema.
    /// </summary>
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<TenantRlsConnectionInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(provider.GetRequiredService<TenantRlsConnectionInterceptor>()));

        services.AddHostedService<RlsRoleGuard>();
        return services;
    }
}
