using Domain.Entities.Example;
using Infrastructure.PostgreSql;

namespace Infrastructure.Persistence.SQLDB.Main.Example;

public sealed class ExampleUsersSql
{
    private readonly MainDapperDbConnection _db;

    public ExampleUsersSql(MainDapperDbConnection db) => _db = db;

    public Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<ExampleUser>(
            """
            SELECT Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.ExampleUsers
            WHERE PublicId = @publicId;
            """,
            new { publicId },
            cancellationToken: ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM dbo.ExampleUsers
                WHERE LOWER(Email) = LOWER(@email)
            );
            """,
            new { email },
            cancellationToken: ct);

    public Task<IEnumerable<ExampleUser>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default) =>
        _db.QueryAsync<ExampleUser>(
            """
            SELECT Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.ExampleUsers
            ORDER BY CreatedAtUtc DESC
            LIMIT @pageSize OFFSET @offset;
            """,
            new { offset = (page - 1) * pageSize, pageSize },
            cancellationToken: ct);

    public Task<int> GetCountAsync(CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM dbo.ExampleUsers;
            """,
            cancellationToken: ct);

    public async Task<ExampleUser> InsertAsync(
        string fullName, string email, string department, string notes,
        CancellationToken ct = default) =>
        (await _db.QueryFirstAsync<ExampleUser>(
            """
            INSERT INTO dbo.ExampleUsers (FullName, Email, Department, Notes)
            VALUES (@fullName, @email, @department, @notes)
            RETURNING Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc;
            """,
            new { fullName, email, department, notes },
            cancellationToken: ct).ConfigureAwait(false))!;

    public Task<int> UpdateAsync(
        Guid publicId, string fullName, string department, string notes,
        CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.ExampleUsers
            SET FullName     = @fullName,
                Department   = @department,
                Notes        = @notes,
                UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId
              AND IsActive  = TRUE;
            """,
            new { publicId, fullName, department, notes },
            cancellationToken: ct);

    public Task<int> DisableAsync(Guid publicId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.ExampleUsers
            SET IsActive     = FALSE,
                UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId
              AND IsActive  = TRUE;
            """,
            new { publicId },
            cancellationToken: ct);
}
