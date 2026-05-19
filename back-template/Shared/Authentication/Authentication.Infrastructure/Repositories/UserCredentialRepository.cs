using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Database;

namespace Authentication.Infrastructure.Repositories;

public sealed class UserCredentialRepository : IUserCredentialRepository
{
    private readonly AppDbContext _db;

    public UserCredentialRepository(AppDbContext db) => _db = db;

    public Task<UserCredential?> GetForLoginAsync(string email, CancellationToken cancellationToken = default) =>
        _db.Credentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Email.ToLower() == email.ToLower(), cancellationToken);

    public Task<UserCredential?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Credentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _db.Credentials
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Email.ToLower() == email.ToLower(), cancellationToken);

    public async Task<UserCredential> InsertAsync(long tenantId, string email, string passwordHash, string role, CancellationToken cancellationToken = default)
    {
        var credential = new UserCredential
        {
            TenantId     = tenantId,
            Email        = email,
            PasswordHash = passwordHash,
            Role         = role,
        };

        _db.Credentials.Add(credential);
        await _db.SaveChangesAsync(cancellationToken);

        return credential;
    }
}
