using Common.MultiTenancy;
using Host.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Context;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>
/// Un Postgres de verdad por corrida, preparado EXACTAMENTE como produccion:
///
/// <list type="number">
///   <item>los dos roles (<c>backtemplate_owner</c> con BYPASSRLS para sembrar, <c>backtemplate_app</c> sin
///   el) y la base, como hace <c>scripts/db/provision-devstack.sql</c>;</item>
///   <item>los permisos de la app con el script real <c>000_app_role_grants.sql</c>, como el dueno;</item>
///   <item>el esquema con las MIGRACIONES reales (no <c>EnsureCreated</c>), como el dueno;</item>
///   <item>las policies con el script real <c>001_enable_rls.sql</c>.</item>
/// </list>
///
/// Las pruebas se conectan con <see cref="AppConnectionString"/>: el rol de la aplicacion. Probar como
/// superusuario daria un aislamiento falso, porque un superusuario ignora RLS.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Database = "backtemplate";
    public const string OwnerRole = "backtemplate_owner";
    public const string AppRole = "backtemplate_app";
    private const string OwnerPassword = "owner-test-pass";
    private const string AppPassword = "app-test-pass";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase(Database)
        .WithUsername("postgres")
        .WithPassword("postgres-test-pass")
        .Build();

    public string SuperuserConnectionString { get; private set; } = string.Empty;
    public string OwnerConnectionString { get; private set; } = string.Empty;
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        SuperuserConnectionString = _container.GetConnectionString();
        OwnerConnectionString = WithUser(OwnerRole, OwnerPassword);
        AppConnectionString = WithUser(AppRole, AppPassword);

        await ExecuteAsync(SuperuserConnectionString, $"""
            CREATE ROLE {OwnerRole} LOGIN NOSUPERUSER BYPASSRLS PASSWORD '{OwnerPassword}';
            CREATE ROLE {AppRole} LOGIN NOSUPERUSER NOBYPASSRLS PASSWORD '{AppPassword}';
            ALTER DATABASE {Database} OWNER TO {OwnerRole};
            REVOKE ALL ON DATABASE {Database} FROM PUBLIC;
            GRANT CONNECT ON DATABASE {Database} TO {AppRole};
            """);

        await ExecuteAsync(OwnerConnectionString, ReadSql("000_app_role_grants.sql"));

        await using (var db = CreateDbContext(OwnerConnectionString))
            await db.Database.MigrateAsync();

        var rls = SqlPath("001_enable_rls.sql");
        if (File.Exists(rls))
            await ExecuteAsync(OwnerConnectionString, await File.ReadAllTextAsync(rls));
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Un <see cref="AppDbContext"/> con todos los modulos, conectado como el rol indicado (por omision, la app).
    /// El tenant y el usuario se controlan con los accessors que se pasan.
    /// </summary>
    public AppDbContext CreateDbContext(
        string? connectionString = null, ITenantContextAccessor? tenant = null, ICurrentUser? user = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString ?? AppConnectionString)
            .Options;

        return new AppDbContext(options, tenant ?? new TenantContextAccessor(), user ?? NoCurrentUser.Instance,
            AppDbContextFactory.Modules);
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<T?> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    public static string SqlPath(string file) => Path.Combine(AppContext.BaseDirectory, "sql", file);

    public static string ReadSql(string file) => File.ReadAllText(SqlPath(file));

    private string WithUser(string user, string password) =>
        new NpgsqlConnectionStringBuilder(SuperuserConnectionString) { Username = user, Password = password }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
