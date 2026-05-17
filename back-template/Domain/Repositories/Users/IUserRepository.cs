using Domain.Entities.Users;

namespace Domain.Repositories.Users;

public interface IUserRepository
{
    Task<User?> GetForLoginAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<User?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<User>> GetPagedAsync(long tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<User> InsertAsync(long tenantId, long branchId, string fullName, string email, string passwordHash, string role, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken cancellationToken = default);
    Task<bool> DisableAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default);
}
