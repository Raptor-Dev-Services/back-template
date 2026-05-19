using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Database;

namespace Authentication.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db) => _db = db;

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Token == token, cancellationToken);

    public async Task InsertAsync(long credentialId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var refreshToken = new RefreshToken
        {
            CredentialId = credentialId,
            Token        = token,
            ExpiresAtUtc = expiresAtUtc,
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var rows = await _db.RefreshTokens
            .Where(e => e.Token == token)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsRevoked, true), cancellationToken);

        return rows > 0;
    }

    public Task RevokeAllByCredentialIdAsync(long credentialId, CancellationToken cancellationToken = default) =>
        _db.RefreshTokens
            .Where(e => e.CredentialId == credentialId && !e.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsRevoked, true), cancellationToken);
}
