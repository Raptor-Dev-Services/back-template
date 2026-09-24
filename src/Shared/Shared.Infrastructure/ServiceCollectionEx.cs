using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace Shared.Infrastructure;

public static class ServiceCollectionEx
{
    /// <summary>
    /// Registra el <see cref="AppDbContext"/> contra Postgres. La cadena es la de la APLICACION (rol sin
    /// DDL y sin BYPASSRLS); las migraciones corren aparte con el rol dueno del esquema.
    /// </summary>
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }
}
