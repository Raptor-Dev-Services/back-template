using Tenancy.Domain.Entities;
using Tenancy.Domain.Repositories;
using Tenancy.Infrastructure.Persistence.SQLDB;

namespace Tenancy.Infrastructure.Repositories;

public sealed class BranchRepository : IBranchRepository
{
    private readonly BranchesSql _sql;

    public BranchRepository(BranchesSql sql) => _sql = sql;

    public Task<Branch?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _sql.GetByIdAsync(id, cancellationToken);
}
