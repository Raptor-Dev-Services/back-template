using Common.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.PostgreSql;

/// <summary>
/// Único punto de ejecución SQL del proyecto.
/// Envuelve Dapper con logging automático de rendimiento: Warning ≥300 ms, Error ≥1 s, Critical ≥2 s.
/// Todas las clases <c>...Sql</c> deben inyectar este tipo en lugar de usar Dapper directamente.
/// </summary>
public sealed class MainDapperDbConnection : DapperSqlDbConnectionBase
{
    /// <summary>
    /// Inicializa una nueva instancia de <see cref="MainDapperDbConnection"/>.
    /// </summary>
    /// <param name="factory">Fábrica que abre las conexiones <c>NpgsqlConnection</c>.</param>
    /// <param name="logger">Logger para salida de rendimiento y diagnóstico.</param>
    /// <param name="configuration">Usada para leer <c>CustomLogging:IncludeSqlText</c>.</param>
    public MainDapperDbConnection(
        MainDbConnectionFactory factory,
        ILogger<MainDapperDbConnection> logger,
        IConfiguration configuration)
        : base(factory, logger, configuration.GetValue<bool>("CustomLogging:IncludeSqlText"))
    { }
}
