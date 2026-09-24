using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Users.Domain.Entities;
using Users.Domain.Repositories;

namespace Users.Infrastructure.Repositories;

/// <summary>
/// Las escrituras van por entidades RASTREADAS y <c>SaveChanges</c>, no por <c>ExecuteUpdate</c>: es lo que
/// hace que el DbContext selle la auditoria (quien y cuando) y que la concurrencia optimista aplique.
/// </summary>
internal sealed class UserProfileRepository(AppDbContext db) : IUserProfileRepository
{
    private DbSet<UserProfile> Profiles => db.Set<UserProfile>();

    public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Profiles.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, cancellationToken);

    public Task<UserProfile?> GetForUpdateAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Profiles.FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        await Profiles
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Profiles.CountAsync(e => e.IsActive, cancellationToken);

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        Profiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
