using Authentication.Domain.Entities;

namespace Authentication.Domain.Repositories;

public interface IUserCredentialRepository
{
    Task<UserCredential?> GetForLoginAsync(string email, CancellationToken cancellationToken = default);
    Task<UserCredential?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserCredential> InsertAsync(long tenantId, string email, string passwordHash, string role, CancellationToken cancellationToken = default);
}
