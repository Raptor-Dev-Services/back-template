using Tenancy.Domain.Entities;
using Users.Domain.Entities;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>
/// Siembra con el rol DUENO (BYPASSRLS): se PREPARA con el dueno y se MIDE con la aplicacion. Sembrar con el
/// rol de la app obligaria a que la prueba confie en la misma barrera que quiere medir.
/// </summary>
public static class Seed
{
    public static async Task<Tenant> TenantAsync(PostgresFixture pg, TenantStatus status = TenantStatus.Active)
    {
        await using var db = pg.CreateDbContext(pg.OwnerConnectionString);
        var tenant = new Tenant
        {
            Name = "Empresa de prueba",
            Slug = $"t-{Guid.NewGuid():N}"[..24],
            Status = status,
        };
        db.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    public static async Task<UserProfile> ProfileAsync(PostgresFixture pg, long tenantId, string fullName = "Persona de prueba")
    {
        await using var db = pg.CreateDbContext(pg.OwnerConnectionString, new FixedTenant(tenantId));
        var profile = new UserProfile { PublicId = Guid.NewGuid(), FullName = fullName };
        db.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }
}
