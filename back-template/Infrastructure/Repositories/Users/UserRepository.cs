using Domain.Entities.Users;
using Domain.Repositories.Users;
using Infrastructure.Persistence.SQLDB.Main.Users;

namespace Infrastructure.Repositories.Users;

public sealed class UserRepository : IUserRepository
{
    private readonly UsersSql _sql;

    public UserRepository(UsersSql sql) => _sql = sql;

    public Task<User?> GetForLoginAsync(string email, CancellationToken cancellationToken = default) =>
        _sql.GetForLoginAsync(email, cancellationToken);

    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _sql.GetByIdAsync(id, cancellationToken);

    public Task<User?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default) =>
        _sql.GetByPublicIdAsync(publicId, tenantId, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _sql.ExistsByEmailAsync(email, cancellationToken);

    public async Task<IReadOnlyCollection<User>> GetPagedAsync(long tenantId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        (await _sql.GetPagedAsync(tenantId, page, pageSize, cancellationToken)).ToArray();

    public Task<int> GetCountAsync(long tenantId, CancellationToken cancellationToken = default) =>
        _sql.GetCountAsync(tenantId, cancellationToken);

    public Task<User> InsertAsync(long tenantId, long branchId, string fullName, string email, string passwordHash, string role, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(tenantId, branchId, fullName, email, passwordHash, role, cancellationToken);

    public async Task<bool> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken cancellationToken = default) =>
        await _sql.UpdateAsync(publicId, tenantId, fullName, cancellationToken) > 0;

    public async Task<bool> DisableAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default) =>
        await _sql.DisableAsync(publicId, tenantId, cancellationToken) > 0;
}
