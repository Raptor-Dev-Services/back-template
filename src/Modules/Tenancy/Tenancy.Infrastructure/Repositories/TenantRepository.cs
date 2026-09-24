using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Tenancy.Domain.Entities;
using Tenancy.Domain.Repositories;

namespace Tenancy.Infrastructure.Repositories;

internal sealed class TenantRepository(AppDbContext db) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        db.Set<Tenant>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        db.Set<Tenant>().AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug, cancellationToken);

    public async Task<Tenant> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant;
    }
}
