using Authentication.Domain.Entities;
using Shared.Database;

namespace Authentication.Infrastructure.Persistence.SQLDB;

public sealed class CredentialsSql
{
    private readonly DapperDbConnection<MainDbConnection> _db;

    public CredentialsSql(DapperDbConnection<MainDbConnection> db) => _db = db;

    public Task<UserCredential?> GetForLoginAsync(string email, CancellationToken ct = default) =>
        _db.QuerySingleAsync<UserCredential>(
            """
            SELECT Id, PublicId, TenantId, BranchId, Email, PasswordHash, Role, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Credentials
            WHERE LOWER(Email) = LOWER(@email);
            """,
            new { email },
            cancellationToken: ct);

    public Task<UserCredential?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.QuerySingleAsync<UserCredential>(
            """
            SELECT Id, PublicId, TenantId, BranchId, Email, PasswordHash, Role, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Credentials
            WHERE Id = @id;
            """,
            new { id },
            cancellationToken: ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(SELECT 1 FROM dbo.Credentials WHERE LOWER(Email) = LOWER(@email));
            """,
            new { email },
            cancellationToken: ct);

    public Task<UserCredential> InsertAsync(long tenantId, long branchId, string email, string passwordHash, string role, CancellationToken ct = default) =>
        _db.QueryFirstAsync<UserCredential>(
            """
            INSERT INTO dbo.Credentials (TenantId, BranchId, Email, PasswordHash, Role)
            VALUES (@tenantId, @branchId, @email, @passwordHash, @role)
            RETURNING Id, PublicId, TenantId, BranchId, Email, PasswordHash, Role, IsActive, CreatedAtUtc, UpdatedAtUtc;
            """,
            new { tenantId, branchId, email, passwordHash, role },
            cancellationToken: ct)!;
}
