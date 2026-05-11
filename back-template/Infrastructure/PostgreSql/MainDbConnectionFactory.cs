using Common.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.PostgreSql;

public sealed class MainDbConnectionFactory : ConfigurationNpgsqlConnectionFactory<MainDbConnection>
{
    public MainDbConnectionFactory(IConfiguration configuration) : base(configuration) { }
}
