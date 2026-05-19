using Tenancy.Contracts.Dtos;

namespace Tenancy.Contracts.Interfaces;

public interface ITenancyApi
{
    Task<TenantDto?> GetTenantByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<BranchDto?> GetBranchByIdAsync(long id, CancellationToken cancellationToken = default);
}
