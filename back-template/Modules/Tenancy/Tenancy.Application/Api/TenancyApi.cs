using Tenancy.Contracts.Dtos;
using Tenancy.Contracts.Interfaces;
using Tenancy.Domain.Repositories;

namespace Tenancy.Application.Api;

public sealed class TenancyApi : ITenancyApi
{
    private readonly ITenantRepository _tenants;

    public TenancyApi(ITenantRepository tenants) => _tenants = tenants;

    public async Task<TenantDto?> GetTenantByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        return tenant is null ? null : new TenantDto(tenant.PublicId, tenant.Name, tenant.Slug, tenant.IsActive);
    }
}
