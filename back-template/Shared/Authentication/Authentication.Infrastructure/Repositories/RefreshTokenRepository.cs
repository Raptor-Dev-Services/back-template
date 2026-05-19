using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Authentication.Infrastructure.Persistence.SQLDB;

namespace Authentication.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly RefreshTokensSql _sql;

    public RefreshTokenRepository(RefreshTokensSql sql) => _sql = sql;

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        _sql.GetByTokenAsync(token, cancellationToken);

    public Task InsertAsync(long credentialId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(credentialId, token, expiresAtUtc, cancellationToken);

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default) =>
        await _sql.RevokeAsync(token, cancellationToken) > 0;

    public Task RevokeAllByCredentialIdAsync(long credentialId, CancellationToken cancellationToken = default) =>
        _sql.RevokeAllByCredentialIdAsync(credentialId, cancellationToken);
}
