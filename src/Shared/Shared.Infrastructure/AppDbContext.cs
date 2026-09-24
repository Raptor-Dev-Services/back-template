using Authentication.Domain.Entities;
using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Tenancy.Domain.Entities;
using Users.Domain.Entities;

namespace Shared.Infrastructure;

public sealed class AppDbContext : DbContext
{
    private readonly ITenantContextAccessor _tenantAccessor;

    public DbSet<Tenant>         Tenants       { get; set; } = null!;
    public DbSet<UserCredential> Credentials   { get; set; } = null!;
    public DbSet<RefreshToken>   RefreshTokens { get; set; } = null!;
    public DbSet<UserProfile>    UserProfiles  { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContextAccessor tenantAccessor)
        : base(options)
    {
        _tenantAccessor = tenantAccessor;
    }

    private long CurrentTenantId =>
        long.TryParse(_tenantAccessor.Current?.TenantId, out var id) ? id : 0L;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("dbo");
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        mb.Entity<UserCredential>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        mb.Entity<UserProfile>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }
}
