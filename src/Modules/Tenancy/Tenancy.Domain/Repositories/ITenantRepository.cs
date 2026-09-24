using Tenancy.Domain.Entities;

namespace Tenancy.Domain.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Tenant> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
