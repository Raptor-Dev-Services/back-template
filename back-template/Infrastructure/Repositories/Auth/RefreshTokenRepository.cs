using Domain.Entities.Auth;
using Domain.Repositories.Auth;
using Infrastructure.Persistence.SQLDB.Main.Auth;

namespace Infrastructure.Repositories.Auth;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly RefreshTokensSql _sql;

    public RefreshTokenRepository(RefreshTokensSql sql) => _sql = sql;

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        _sql.GetByTokenAsync(token, cancellationToken);

    public Task InsertAsync(long userId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(userId, token, expiresAtUtc, cancellationToken);

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default) =>
        await _sql.RevokeAsync(token, cancellationToken) > 0;

    public Task RevokeAllByUserIdAsync(long userId, CancellationToken cancellationToken = default) =>
        _sql.RevokeAllByUserIdAsync(userId, cancellationToken);
}
