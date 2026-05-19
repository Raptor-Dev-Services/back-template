using Shared.Database;
using Tenancy.Domain.Entities;

namespace Tenancy.Infrastructure.Persistence.SQLDB;

public sealed class BranchesSql
{
    private readonly DapperDbConnection<MainDbConnection> _db;

    public BranchesSql(DapperDbConnection<MainDbConnection> db) => _db = db;

    public Task<Branch?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.QuerySingleAsync<Branch>(
            """
            SELECT Id, PublicId, TenantId, Name, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Branches
            WHERE Id = @id AND IsActive = TRUE;
            """,
            new { id },
            cancellationToken: ct);
}
