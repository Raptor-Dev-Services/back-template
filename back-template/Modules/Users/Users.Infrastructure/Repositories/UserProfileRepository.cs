using Users.Domain.Entities;
using Users.Domain.Repositories;
using Users.Infrastructure.Persistence.SQLDB;

namespace Users.Infrastructure.Repositories;

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly UserProfilesSql _sql;

    public UserProfileRepository(UserProfilesSql sql) => _sql = sql;

    public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default) =>
        _sql.GetByPublicIdAsync(publicId, tenantId, cancellationToken);

    public async Task<IReadOnlyCollection<UserProfile>> GetPagedAsync(long tenantId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        (await _sql.GetPagedAsync(tenantId, page, pageSize, cancellationToken)).ToArray();

    public Task<int> GetCountAsync(long tenantId, CancellationToken cancellationToken = default) =>
        _sql.GetCountAsync(tenantId, cancellationToken);

    public Task InsertAsync(Guid publicId, long tenantId, long branchId, string fullName, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(publicId, tenantId, branchId, fullName, cancellationToken);

    public async Task<bool> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken cancellationToken = default) =>
        await _sql.UpdateAsync(publicId, tenantId, fullName, cancellationToken) > 0;

    public async Task<bool> DisableAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default) =>
        await _sql.DisableAsync(publicId, tenantId, cancellationToken) > 0;
}
