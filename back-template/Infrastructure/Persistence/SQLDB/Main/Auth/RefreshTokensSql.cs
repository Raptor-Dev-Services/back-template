using Domain.Entities.Auth;
using Infrastructure.PostgreSql;

namespace Infrastructure.Persistence.SQLDB.Main.Auth;

public sealed class RefreshTokensSql
{
    private readonly MainDapperDbConnection _db;

    public RefreshTokensSql(MainDapperDbConnection db) => _db = db;

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        _db.QuerySingleAsync<RefreshToken>(
            """
            SELECT Id, UserId, Token, ExpiresAtUtc, IsRevoked, CreatedAtUtc
            FROM dbo.RefreshTokens
            WHERE Token = @token;
            """,
            new { token },
            cancellationToken: ct);

    public Task InsertAsync(long userId, string token, DateTime expiresAtUtc, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            INSERT INTO dbo.RefreshTokens (UserId, Token, ExpiresAtUtc)
            VALUES (@userId, @token, @expiresAtUtc);
            """,
            new { userId, token, expiresAtUtc },
            cancellationToken: ct);

    public Task<int> RevokeAsync(string token, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.RefreshTokens SET IsRevoked = TRUE WHERE Token = @token;
            """,
            new { token },
            cancellationToken: ct);

    public Task RevokeAllByUserIdAsync(long userId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.RefreshTokens SET IsRevoked = TRUE WHERE UserId = @userId AND IsRevoked = FALSE;
            """,
            new { userId },
            cancellationToken: ct);
}
