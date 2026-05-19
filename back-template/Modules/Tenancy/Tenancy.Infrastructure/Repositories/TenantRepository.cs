using Tenancy.Domain.Entities;
using Tenancy.Domain.Repositories;
using Tenancy.Infrastructure.Persistence.SQLDB;

namespace Tenancy.Infrastructure.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly TenantsSql _sql;

    public TenantRepository(TenantsSql sql) => _sql = sql;

    public Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _sql.GetByIdAsync(id, cancellationToken);
}
