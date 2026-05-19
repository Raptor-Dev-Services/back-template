using Common.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Database;

public sealed class DapperDbConnection<T> : DapperSqlDbConnectionBase where T : class
{
    public DapperDbConnection(
        DbConnectionFactory<T> factory,
        ILogger<DapperDbConnection<T>> logger,
        IConfiguration configuration)
        : base(factory, logger, configuration.GetValue<bool>("CustomLogging:IncludeSqlText"))
    { }
}
