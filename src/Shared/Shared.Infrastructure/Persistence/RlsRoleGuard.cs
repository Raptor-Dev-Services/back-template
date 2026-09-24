using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Comprueba AL ARRANCAR que la aplicacion se conecta con un rol que RLS puede gobernar.
///
/// <para>El aislamiento entre tenants se sostiene sobre dos barreras: el filtro de EF y las policies de RLS.
/// La segunda deja de existir -sin error, sin log, sin health check en rojo- si la cadena de conexion apunta a
/// un superusuario o a un rol con BYPASSRLS, porque Postgres se las salta por definicion. Todo seguiria
/// pasando: el arranque, las pruebas y el filtro de EF, que tapa el hueco en casi todas las consultas. El
/// resto -cualquier <c>IgnoreQueryFilters</c>, cualquier SQL crudo- queda sin barrera.</para>
///
/// <para>Por eso es fail-fast y no un aviso: un aviso en el log es exactamente como este fallo pasa
/// desapercibido.</para>
/// </summary>
public sealed class RlsRoleGuard(IServiceScopeFactory scopes, ILogger<RlsRoleGuard> logger) : IHostedService
{
    /// <summary>La REGLA, separada del acceso a datos para poder probarla sin base de por medio.</summary>
    public static void EnsureRoleCanBeGovernedByRls(string roleName, bool isSuperuser, bool bypassesRls)
    {
        if (!isSuperuser && !bypassesRls)
            return;

        var reason = isSuperuser ? "es superusuario" : "tiene BYPASSRLS";
        throw new InvalidOperationException(
            $"El rol de conexion '{roleName}' {reason}: Postgres ignora las policies de RLS y el aislamiento entre " +
            "tenants quedaria sostenido solo por el filtro de EF. Conecta la API con el rol de la aplicacion " +
            "(backtemplate_app, sin BYPASSRLS). Las migraciones usan el rol dueno, pero por otra cadena.");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Se pregunta por la conexion REAL de la aplicacion, no por una cadena leida aparte.
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_user, rolsuper, rolbypassrls FROM pg_roles WHERE rolname = current_user";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("No se pudo leer el rol de conexion en pg_roles para verificar que RLS aplica.");

            var role = reader.GetString(0);
            EnsureRoleCanBeGovernedByRls(role, reader.GetBoolean(1), reader.GetBoolean(2));
            logger.LogInformation("RLS activo: la aplicacion se conecta como {Role}, sin BYPASSRLS.", role);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
