using Shared.Database;
using Tenancy.Domain.Entities;

namespace Tenancy.Infrastructure.Persistence.SQLDB;

public sealed class TenantsSql
{
    private readonly DapperDbConnection<MainDbConnection> _db;

    public TenantsSql(DapperDbConnection<MainDbConnection> db) => _db = db;

    public Task<Tenant?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.QuerySingleAsync<Tenant>(
            """
            SELECT Id, PublicId, Name, Slug, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Tenants
            WHERE Id = @id AND IsActive = TRUE;
            """,
            new { id },
            cancellationToken: ct);
}
