using Shared.Database;
using Users.Domain.Entities;

namespace Users.Infrastructure.Persistence.SQLDB;

public sealed class UserProfilesSql
{
    private readonly DapperDbConnection<MainDbConnection> _db;

    public UserProfilesSql(DapperDbConnection<MainDbConnection> db) => _db = db;

    public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<UserProfile>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.UserProfiles
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId },
            cancellationToken: ct);

    public Task<IEnumerable<UserProfile>> GetPagedAsync(long tenantId, int page, int pageSize, CancellationToken ct = default) =>
        _db.QueryAsync<UserProfile>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.UserProfiles
            WHERE TenantId = @tenantId AND IsActive = TRUE
            ORDER BY CreatedAtUtc DESC
            LIMIT @limit OFFSET @offset;
            """,
            new { tenantId, limit = pageSize, offset = (page - 1) * pageSize },
            cancellationToken: ct);

    public Task<int> GetCountAsync(long tenantId, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM dbo.UserProfiles WHERE TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { tenantId },
            cancellationToken: ct);

    public Task InsertAsync(Guid publicId, long tenantId, long branchId, string fullName, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            INSERT INTO dbo.UserProfiles (PublicId, TenantId, BranchId, FullName)
            VALUES (@publicId, @tenantId, @branchId, @fullName);
            """,
            new { publicId, tenantId, branchId, fullName },
            cancellationToken: ct);

    public Task<int> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.UserProfiles
            SET FullName = @fullName, UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId, fullName },
            cancellationToken: ct);

    public Task<int> DisableAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.UserProfiles
            SET IsActive = FALSE, UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId },
            cancellationToken: ct);
}
