using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Authentication.Infrastructure.Persistence.SQLDB;

namespace Authentication.Infrastructure.Repositories;

public sealed class UserCredentialRepository : IUserCredentialRepository
{
    private readonly CredentialsSql _sql;

    public UserCredentialRepository(CredentialsSql sql) => _sql = sql;

    public Task<UserCredential?> GetForLoginAsync(string email, CancellationToken cancellationToken = default) =>
        _sql.GetForLoginAsync(email, cancellationToken);

    public Task<UserCredential?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _sql.GetByIdAsync(id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _sql.ExistsByEmailAsync(email, cancellationToken);

    public Task<UserCredential> InsertAsync(long tenantId, long branchId, string email, string passwordHash, string role, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(tenantId, branchId, email, passwordHash, role, cancellationToken);
}
