using System.Data.Common;
using System.Globalization;
using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Alimenta la segunda barrera de aislamiento: fija el GUC de sesion <c>app.tenant_id</c> en CADA conexion
/// que abre el DbContext, para que las policies de RLS (<c>Persistence/Sql/001_enable_rls.sql</c>) filtren a
/// nivel de motor.
///
/// <para><b>Parametrizado.</b> <c>set_config('app.tenant_id', @tenant, false)</c>, nunca un <c>SET</c> con el
/// valor interpolado: el tenant sale de un claim y no se concatena en SQL aunque sea "de confianza".</para>
///
/// <para><b>En cada apertura, no al cerrar.</b> Npgsql recicla conexiones fisicas del pool. <c>ConnectionOpened</c>
/// dispara tambien cuando EF toma una del pool, asi que el GUC se SOBREESCRIBE antes de la primera consulta
/// de esta peticion: una conexion reciclada nunca sirve datos con el tenant del uso anterior. Sin tenant en
/// contexto se fija a vacio (fail-closed: cero filas en las tablas con policy).</para>
/// </summary>
public sealed class TenantRlsConnectionInterceptor(ITenantContextAccessor tenant) : DbConnectionInterceptor
{
    private const string SetTenantSql = "SELECT set_config('app.tenant_id', @tenant, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = SetTenantSql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tenant";
        // Solo se acepta un entero: cualquier otra cosa en el contexto (no deberia) se trata como "sin tenant".
        parameter.Value = long.TryParse(tenant.Current?.TenantId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        command.Parameters.Add(parameter);

        return command;
    }
}
