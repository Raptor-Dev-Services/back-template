using Users.Domain.Entities;

namespace Users.Domain.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserProfile>> GetPagedAsync(long tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(long tenantId, CancellationToken cancellationToken = default);
    Task InsertAsync(Guid publicId, long tenantId, string fullName, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken cancellationToken = default);
    Task<bool> DisableAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default);
}
