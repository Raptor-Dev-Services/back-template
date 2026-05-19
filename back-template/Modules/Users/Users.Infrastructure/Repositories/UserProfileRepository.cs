using Microsoft.EntityFrameworkCore;
using Shared.Database;
using Users.Domain.Entities;
using Users.Domain.Repositories;

namespace Users.Infrastructure.Repositories;

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _db;

    public UserProfileRepository(AppDbContext db) => _db = db;

    public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default) =>
        _db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, cancellationToken);

    public async Task<IReadOnlyCollection<UserProfile>> GetPagedAsync(long tenantId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        await _db.UserProfiles
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

    public Task<int> GetCountAsync(long tenantId, CancellationToken cancellationToken = default) =>
        _db.UserProfiles
            .CountAsync(e => e.IsActive, cancellationToken);

    public async Task InsertAsync(Guid publicId, long tenantId, string fullName, CancellationToken cancellationToken = default)
    {
        var profile = new UserProfile
        {
            PublicId = publicId,
            TenantId = tenantId,
            FullName = fullName,
        };

        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken cancellationToken = default)
    {
        var rows = await _db.UserProfiles
            .Where(e => e.PublicId == publicId && e.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.FullName,     fullName)
                .SetProperty(e => e.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);

        return rows > 0;
    }

    public async Task<bool> DisableAsync(Guid publicId, long tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.UserProfiles
            .Where(e => e.PublicId == publicId && e.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.IsActive,     false)
                .SetProperty(e => e.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);

        return rows > 0;
    }
}
