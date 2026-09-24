using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Authentication.Infrastructure.Repositories;

/// <summary>
/// La autenticacion corre ANTES de conocer el tenant, asi que sus consultas ignoran SOLO el filtro de tenant
/// (<see cref="QueryFilterNames.Tenant"/>) y conservan el de soft delete. Siempre por claves unicas en todo el
/// sistema (correo, id, hash), para que no puedan traer la fila de otro tenant por accidente.
/// </summary>
internal sealed class UserCredentialRepository(AppDbContext db) : IUserCredentialRepository
{
    private DbSet<UserCredential> Set => db.Set<UserCredential>();

    private IQueryable<UserCredential> AcrossTenants => Set.IgnoreQueryFilters([QueryFilterNames.Tenant]);

    public Task<UserCredential?> FindForSignInAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        AcrossTenants.FirstOrDefaultAsync(e => e.Email == normalizedEmail, cancellationToken);

    public Task<UserCredential?> FindForSignInAsync(long credentialId, CancellationToken cancellationToken = default) =>
        AcrossTenants.FirstOrDefaultAsync(e => e.Id == credentialId, cancellationToken);

    public Task<UserCredential?> FindForSignInAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        AcrossTenants.FirstOrDefaultAsync(e => e.PublicId == publicId, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters().AnyAsync(e => e.Email == normalizedEmail, cancellationToken);

    public Task<bool> AnyInTenantAsync(long tenantId, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters().AnyAsync(e => e.TenantId == tenantId, cancellationToken);

    public Task<UserCredential?> FindInTenantAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.PublicId == publicId, cancellationToken);

    public void Add(UserCredential credential) => Set.Add(credential);
}

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    private IQueryable<RefreshToken> AcrossTenants => db.Set<RefreshToken>().IgnoreQueryFilters([QueryFilterNames.Tenant]);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        AcrossTenants.FirstOrDefaultAsync(e => e.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => db.Set<RefreshToken>().Add(token);

    public async Task<int> RevokeAllActiveAsync(long credentialId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default)
    {
        // Entidades rastreadas y no ExecuteUpdate: la revocacion queda auditada (quien, cuando) como cualquier escritura.
        var active = await AcrossTenants
            .Where(e => e.CredentialId == credentialId && e.RevokedAtUtc == null && e.ExpiresAtUtc > nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
            token.Revoke(nowUtc, reason);

        return active.Count;
    }
}

internal sealed class PasswordSetupTokenRepository(AppDbContext db) : IPasswordSetupTokenRepository
{
    private IQueryable<PasswordSetupToken> AcrossTenants => db.Set<PasswordSetupToken>().IgnoreQueryFilters([QueryFilterNames.Tenant]);

    public Task<PasswordSetupToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        AcrossTenants.FirstOrDefaultAsync(e => e.TokenHash == tokenHash, cancellationToken);

    public async Task InvalidateActiveAsync(long credentialId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var active = await AcrossTenants
            .Where(e => e.CredentialId == credentialId && e.ConsumedAtUtc == null && e.ExpiresAtUtc > nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
            token.Consume(nowUtc);
    }

    public void Add(PasswordSetupToken token) => db.Set<PasswordSetupToken>().Add(token);
}

internal sealed class TwoFactorRecoveryCodeRepository(AppDbContext db) : ITwoFactorRecoveryCodeRepository
{
    private IQueryable<TwoFactorRecoveryCode> AcrossTenants => db.Set<TwoFactorRecoveryCode>().IgnoreQueryFilters([QueryFilterNames.Tenant]);

    public async Task<IReadOnlyList<TwoFactorRecoveryCode>> ListUsableAsync(long credentialId, CancellationToken cancellationToken = default) =>
        await AcrossTenants.Where(c => c.CredentialId == credentialId && c.ConsumedAtUtc == null).ToListAsync(cancellationToken);

    public async Task ConsumeAllAsync(long credentialId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        foreach (var code in await ListUsableAsync(credentialId, cancellationToken))
            code.Consume(nowUtc);
    }

    public void Add(TwoFactorRecoveryCode code) => db.Set<TwoFactorRecoveryCode>().Add(code);
}
