using Tenancy.Contracts.Dtos;
using Tenancy.Contracts.Interfaces;
using Tenancy.Domain.Repositories;

namespace Tenancy.Application.Api;

public sealed class TenancyApi : ITenancyApi
{
    private readonly ITenantRepository _tenants;
    private readonly IBranchRepository _branches;

    public TenancyApi(ITenantRepository tenants, IBranchRepository branches)
    {
        _tenants  = tenants;
        _branches = branches;
    }

    public async Task<TenantDto?> GetTenantByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        return tenant is null ? null : new TenantDto(tenant.PublicId, tenant.Name, tenant.Slug, tenant.IsActive);
    }

    public async Task<BranchDto?> GetBranchByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.GetByIdAsync(id, cancellationToken);
        return branch is null ? null : new BranchDto(branch.PublicId, branch.Name, branch.TenantId, branch.IsActive);
    }
}
