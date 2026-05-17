using Domain.Entities.Users;
using Infrastructure.PostgreSql;

namespace Infrastructure.Persistence.SQLDB.Main.Users;

public sealed class UsersSql
{
    private readonly MainDapperDbConnection _db;

    public UsersSql(MainDapperDbConnection db) => _db = db;

    public Task<User?> GetForLoginAsync(string email, CancellationToken ct = default) =>
        _db.QuerySingleAsync<User>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, Email, PasswordHash, Role, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Users
            WHERE LOWER(Email) = LOWER(@email);
            """,
            new { email },
            cancellationToken: ct);

    public Task<User?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.QuerySingleAsync<User>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, Email, PasswordHash, Role, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Users
            WHERE Id = @id;
            """,
            new { id },
            cancellationToken: ct);

    public Task<User?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<User>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, Email, Role, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Users
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId },
            cancellationToken: ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(SELECT 1 FROM dbo.Users WHERE LOWER(Email) = LOWER(@email));
            """,
            new { email },
            cancellationToken: ct);

    public Task<IEnumerable<User>> GetPagedAsync(long tenantId, int page, int pageSize, CancellationToken ct = default) =>
        _db.QueryAsync<User>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, Email, Role, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Users
            WHERE TenantId = @tenantId AND IsActive = TRUE
            ORDER BY CreatedAtUtc DESC
            LIMIT @limit OFFSET @offset;
            """,
            new { tenantId, limit = pageSize, offset = (page - 1) * pageSize },
            cancellationToken: ct);

    public Task<int> GetCountAsync(long tenantId, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM dbo.Users WHERE TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { tenantId },
            cancellationToken: ct);

    public Task<User> InsertAsync(long tenantId, long branchId, string fullName, string email, string passwordHash, string role, CancellationToken ct = default) =>
        _db.QueryFirstAsync<User>(
            """
            INSERT INTO dbo.Users (TenantId, BranchId, FullName, Email, PasswordHash, Role)
            VALUES (@tenantId, @branchId, @fullName, @email, @passwordHash, @role)
            RETURNING Id, PublicId, TenantId, BranchId, FullName, Email, Role, IsActive, CreatedAtUtc, UpdatedAtUtc;
            """,
            new { tenantId, branchId, fullName, email, passwordHash, role },
            cancellationToken: ct)!;

    public Task<int> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.Users
            SET FullName = @fullName, UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId, fullName },
            cancellationToken: ct);

    public Task<int> DisableAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.Users
            SET IsActive = FALSE, UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId },
            cancellationToken: ct);
}
