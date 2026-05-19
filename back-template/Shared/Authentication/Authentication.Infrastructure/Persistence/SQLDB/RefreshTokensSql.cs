using Authentication.Domain.Entities;
using Shared.Database;

namespace Authentication.Infrastructure.Persistence.SQLDB;

public sealed class RefreshTokensSql
{
    private readonly DapperDbConnection<MainDbConnection> _db;

    public RefreshTokensSql(DapperDbConnection<MainDbConnection> db) => _db = db;

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        _db.QuerySingleAsync<RefreshToken>(
            """
            SELECT Id, CredentialId, Token, ExpiresAtUtc, IsRevoked, CreatedAtUtc
            FROM dbo.RefreshTokens
            WHERE Token = @token;
            """,
            new { token },
            cancellationToken: ct);

    public Task InsertAsync(long credentialId, string token, DateTime expiresAtUtc, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            INSERT INTO dbo.RefreshTokens (CredentialId, Token, ExpiresAtUtc)
            VALUES (@credentialId, @token, @expiresAtUtc);
            """,
            new { credentialId, token, expiresAtUtc },
            cancellationToken: ct);

    public Task<int> RevokeAsync(string token, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.RefreshTokens SET IsRevoked = TRUE WHERE Token = @token;
            """,
            new { token },
            cancellationToken: ct);

    public Task RevokeAllByCredentialIdAsync(long credentialId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.RefreshTokens SET IsRevoked = TRUE WHERE CredentialId = @credentialId AND IsRevoked = FALSE;
            """,
            new { credentialId },
            cancellationToken: ct);
}
