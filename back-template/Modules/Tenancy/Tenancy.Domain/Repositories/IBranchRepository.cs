using Tenancy.Domain.Entities;

namespace Tenancy.Domain.Repositories;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
