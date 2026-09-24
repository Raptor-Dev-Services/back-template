using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Audit;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Audit;
using Shared.Kernel.Context;

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

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ITenantScope, TenantScope>();

        // Bitacora de acciones: una implementacion, dos puertos (escribir en la transaccion del caso de uso, leer).
        services.AddScoped<EfAuditLog>();
        services.AddScoped<IAuditLog>(provider => provider.GetRequiredService<EfAuditLog>());
        services.AddScoped<IAuditLogReader>(provider => provider.GetRequiredService<EfAuditLog>());

        services.AddHostedService<RlsRoleGuard>();
        return services;
    }
}
