using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shared.Database;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddMainDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("MainDbConnection")));

        services.AddHostedService<DatabaseInitializationService>();

        return services;
    }
}

internal sealed class DatabaseInitializationService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseInitializationService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.Current = new TenantContext("0");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
