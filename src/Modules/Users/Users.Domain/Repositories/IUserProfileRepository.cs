using Users.Domain.Entities;

namespace Users.Domain.Repositories;

/// <summary>
/// Acceso a perfiles. No recibe el tenant: el aislamiento lo aplican el filtro global de EF y la policy de
/// RLS con el tenant del JWT. Un parametro <c>tenantId</c> aqui seria una segunda fuente de verdad que tarde
/// o temprano alguien llena desde el cliente.
/// </summary>
public interface IUserProfileRepository
{
    Task<UserProfile?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

    /// <summary>Version rastreada, para modificarla y guardar con <see cref="SaveChangesAsync"/>.</summary>
    Task<UserProfile?> GetForUpdateAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
