using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure;
using Tenancy.Domain.Entities;
using Tenancy.Domain.Repositories;

namespace Tenancy.Infrastructure.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _db;

    public TenantRepository(AppDbContext db) => _db = db;

    public Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.IsActive, cancellationToken);
}
