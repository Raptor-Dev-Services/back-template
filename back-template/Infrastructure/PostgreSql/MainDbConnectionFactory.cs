using Common.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.PostgreSql;

/// <summary>
/// Fábrica de conexiones <c>NpgsqlConnection</c> para la base de datos principal.
/// Lee la cadena de conexión desde <c>ConnectionStrings:MainDbConnection</c>.
/// </summary>
public sealed class MainDbConnectionFactory : ConfigurationNpgsqlConnectionFactory<MainDbConnection>
{
    /// <summary>
    /// Inicializa una nueva instancia de <see cref="MainDbConnectionFactory"/>.
    /// </summary>
    /// <param name="configuration">Fuente de configuración de la aplicación.</param>
    public MainDbConnectionFactory(IConfiguration configuration) : base(configuration) { }
}
