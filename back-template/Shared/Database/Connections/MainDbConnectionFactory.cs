using Common.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace Shared.Database;

public sealed class MainDbConnectionFactory : ConfigurationNpgsqlConnectionFactory<MainDbConnection>
{
    public MainDbConnectionFactory(IConfiguration configuration) : base(configuration) { }
}
