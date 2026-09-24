using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Authentication.Infrastructure.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    private IQueryable<RefreshToken> AcrossTenants =>
        db.Set<RefreshToken>().IgnoreQueryFilters([QueryFilterNames.Tenant]);

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        AcrossTenants.AsNoTracking().FirstOrDefaultAsync(e => e.Token == token, cancellationToken);

    public async Task InsertAsync(
        long tenantId, long credentialId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        db.Set<RefreshToken>().Add(new RefreshToken
        {
            TenantId = tenantId,
            CredentialId = credentialId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var existing = await AcrossTenants.FirstOrDefaultAsync(e => e.Token == token && !e.IsRevoked, cancellationToken);
        if (existing is null)
            return false;

        existing.IsRevoked = true;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
