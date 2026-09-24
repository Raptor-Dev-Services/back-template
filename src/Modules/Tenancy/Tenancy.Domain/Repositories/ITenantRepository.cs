using Tenancy.Domain.Entities;

namespace Tenancy.Domain.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
