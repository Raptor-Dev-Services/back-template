using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Tenancy;
using Tenancy.Domain.Entities;

namespace Tenancy.Infrastructure.Services;

/// <summary>
/// Estado del tenant con una cache corta en memoria (por replica). Singleton: abre su propio scope para leer, porque
/// el <c>AppDbContext</c> es scoped. Un tenant borrado (soft delete) o inexistente cuenta como NO activo.
/// </summary>
internal sealed class TenantStatusProvider(IServiceScopeFactory scopes, TimeProvider clock) : ITenantStatusProvider
{
    /// <summary>Cuanto puede tardar una suspension en surtir efecto en peticiones con token ya emitido.</summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<long, (bool IsActive, DateTimeOffset ExpiresAt)> _cache = new();

    public async Task<bool> IsActiveAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        if (_cache.TryGetValue(tenantId, out var cached) && cached.ExpiresAt > now)
            return cached.IsActive;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var status = await db.Set<Tenant>().AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => (TenantStatus?)t.Status)
            .FirstOrDefaultAsync(cancellationToken);

        var isActive = status == TenantStatus.Active;
        _cache[tenantId] = (isActive, now + CacheFor);
        return isActive;
    }
}
