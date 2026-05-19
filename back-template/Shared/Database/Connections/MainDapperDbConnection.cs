using Common.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Database;

public sealed class MainDapperDbConnection : DapperSqlDbConnectionBase
{
    public MainDapperDbConnection(
        MainDbConnectionFactory factory,
        ILogger<MainDapperDbConnection> logger,
        IConfiguration configuration)
        : base(factory, logger, configuration.GetValue<bool>("CustomLogging:IncludeSqlText"))
    { }
}
