using Common.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace Shared.Database;

public sealed class DbConnectionFactory<T> : ConfigurationNpgsqlConnectionFactory<T> where T : class
{
    public DbConnectionFactory(IConfiguration configuration) : base(configuration) { }
}
