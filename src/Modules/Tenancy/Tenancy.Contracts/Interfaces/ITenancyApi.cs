using Tenancy.Contracts.Dtos;

namespace Tenancy.Contracts.Interfaces;

/// <summary>
/// La API publica del modulo Tenancy para otros modulos (comunicacion inter-modulo solo via Contracts).
/// </summary>
public interface ITenancyApi
{
    Task<TenantDto?> GetTenantByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<TenantDto?> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta un tenant activo. Lanza <c>ValidationException</c> si el nombre o el slug no son validos y
    /// <c>ConflictException</c> si el slug ya existe.
    /// </summary>
    Task<TenantDto> CreateTenantAsync(string name, string slug, CancellationToken cancellationToken = default);
}
