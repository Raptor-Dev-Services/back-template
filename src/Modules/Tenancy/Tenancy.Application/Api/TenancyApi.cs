using Shared.Kernel.Errors;
using Tenancy.Contracts.Dtos;
using Tenancy.Contracts.Interfaces;
using Tenancy.Domain;
using Tenancy.Domain.Entities;
using Tenancy.Domain.Repositories;

namespace Tenancy.Application.Api;

internal sealed class TenancyApi(ITenantRepository tenants) : ITenancyApi
{
    public async Task<TenantDto?> GetTenantByIdAsync(long id, CancellationToken cancellationToken = default) =>
        ToDto(await tenants.GetByIdAsync(id, cancellationToken));

    public async Task<TenantDto?> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        ToDto(await tenants.GetBySlugAsync(TenantSlug.Normalize(slug), cancellationToken));

    public async Task<TenantDto> CreateTenantAsync(string name, string slug, CancellationToken cancellationToken = default)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedSlug = TenantSlug.Normalize(slug);

        if (normalizedName.Length is 0 or > 200)
            throw new ValidationException("El nombre del tenant es obligatorio y admite hasta 200 caracteres.");
        if (!TenantSlug.IsValid(normalizedSlug))
            throw new ValidationException("El slug admite minusculas, digitos y guiones (3 a 63 caracteres, sin guion en los extremos).");
        if (await tenants.GetBySlugAsync(normalizedSlug, cancellationToken) is not null)
            throw new ConflictException("Ya existe un tenant con ese slug.");

        var tenant = await tenants.InsertAsync(new Tenant { Name = normalizedName, Slug = normalizedSlug }, cancellationToken);
        return ToDto(tenant)!;
    }

    private static TenantDto? ToDto(Tenant? tenant) =>
        tenant is null ? null : new TenantDto(tenant.Id, tenant.PublicId, tenant.Name, tenant.Slug, tenant.IsActive);
}
