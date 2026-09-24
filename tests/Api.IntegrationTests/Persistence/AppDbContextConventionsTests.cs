using Api.IntegrationTests.Infrastructure;
using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Context;
using Shared.Kernel.Errors;
using Tenancy.Domain.Entities;
using Users.Domain.Entities;
using Xunit;

namespace Api.IntegrationTests.Persistence;

/// <summary>
/// Las convenciones del <see cref="AppDbContext"/> contra Postgres real y con el rol de la aplicacion:
/// auditoria, soft delete, UTC, unicidad y concurrencia. Todo lo que EF InMemory no puede demostrar.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AppDbContextConventionsTests(PostgresFixture pg)
{
    private sealed class FixedUser(Guid id, long tenant) : ICurrentUser
    {
        public Guid? UserId => id;
        public long? TenantId => tenant;
    }

    private async Task<long> NewTenantAsync()
    {
        await using var db = pg.CreateDbContext(pg.OwnerConnectionString);
        var tenant = new Tenant { Name = "Empresa", Slug = $"t-{Guid.NewGuid():N}"[..20] };
        db.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static TenantContextAccessor TenantScope(long tenantId) =>
        new() { Current = new TenantContext(tenantId.ToString()) };

    [Fact]
    public async Task Insertar_sella_tenant_auditoria_y_actor_en_utc()
    {
        var tenantId = await NewTenantAsync();
        var actor = Guid.NewGuid();
        var before = DateTime.UtcNow.AddSeconds(-1);

        long id;
        await using (var db = pg.CreateDbContext(tenant: TenantScope(tenantId), user: new FixedUser(actor, tenantId)))
        {
            var profile = new UserProfile { PublicId = Guid.NewGuid(), FullName = "Ana" }; // SIN TenantId
            db.Add(profile);
            await db.SaveChangesAsync();
            id = profile.Id;
        }

        await using (var db = pg.CreateDbContext(tenant: TenantScope(tenantId)))
        {
            var saved = await db.Set<UserProfile>().SingleAsync(p => p.Id == id);
            Assert.Equal(tenantId, saved.TenantId);
            Assert.Equal(actor, saved.CreatedByUserId);
            Assert.Equal(DateTimeKind.Utc, saved.CreatedAtUtc.Kind);
            Assert.True(saved.CreatedAtUtc >= before);
            Assert.Equal(saved.CreatedAtUtc, saved.UpdatedAtUtc);
        }
    }

    [Fact]
    public async Task Remove_es_soft_delete_y_la_fila_desaparece_de_las_consultas()
    {
        var tenantId = await NewTenantAsync();
        var actor = Guid.NewGuid();
        var publicId = Guid.NewGuid();

        await using (var db = pg.CreateDbContext(tenant: TenantScope(tenantId)))
        {
            db.Add(new UserProfile { PublicId = publicId, FullName = "Borrame" });
            await db.SaveChangesAsync();
        }

        await using (var db = pg.CreateDbContext(tenant: TenantScope(tenantId), user: new FixedUser(actor, tenantId)))
        {
            var profile = await db.Set<UserProfile>().SingleAsync(p => p.PublicId == publicId);
            db.Remove(profile);
            await db.SaveChangesAsync();
        }

        await using (var db = pg.CreateDbContext(tenant: TenantScope(tenantId)))
        {
            Assert.False(await db.Set<UserProfile>().AnyAsync(p => p.PublicId == publicId));

            // La fila sigue ahi, marcada y con quien la borro.
            var raw = await db.Set<UserProfile>().IgnoreQueryFilters([QueryFilterNames.SoftDelete])
                .SingleAsync(p => p.PublicId == publicId);
            Assert.True(raw.IsDeleted);
            Assert.NotNull(raw.DeletedAtUtc);
            Assert.Equal(actor, raw.DeletedByUserId);
        }
    }

    [Fact]
    public async Task El_rol_de_la_aplicacion_no_puede_borrar_fisicamente()
    {
        var tenantId = await NewTenantAsync();
        await using var db = pg.CreateDbContext(tenant: TenantScope(tenantId));

        // Un ExecuteDelete se salta la conversion a soft delete; el motor lo rechaza porque la app no tiene DELETE.
        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Set<UserProfile>().ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task Violacion_de_indice_unico_es_conflicto_de_negocio()
    {
        var tenantId = await NewTenantAsync();
        var publicId = Guid.NewGuid();

        await using var db = pg.CreateDbContext(tenant: TenantScope(tenantId));
        db.Add(new UserProfile { PublicId = publicId, FullName = "Uno" });
        await db.SaveChangesAsync();

        db.Add(new UserProfile { PublicId = publicId, FullName = "Dos" });
        var ex = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        Assert.DoesNotContain("UX_", ex.Message); // el nombre del indice no le sirve a quien opera
    }

    [Fact]
    public async Task Perder_una_carrera_de_concurrencia_es_conflicto_de_negocio()
    {
        var tenantId = await NewTenantAsync();
        var publicId = Guid.NewGuid();

        await using (var seed = pg.CreateDbContext(tenant: TenantScope(tenantId)))
        {
            seed.Add(new UserProfile { PublicId = publicId, FullName = "Original" });
            await seed.SaveChangesAsync();
        }

        await using var first = pg.CreateDbContext(tenant: TenantScope(tenantId));
        await using var second = pg.CreateDbContext(tenant: TenantScope(tenantId));
        var a = await first.Set<UserProfile>().SingleAsync(p => p.PublicId == publicId);
        var b = await second.Set<UserProfile>().SingleAsync(p => p.PublicId == publicId);

        a.FullName = "Gana";
        await first.SaveChangesAsync();

        b.FullName = "Pierde";
        await Assert.ThrowsAsync<ConflictException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Insertar_una_fila_tenant_aware_sin_tenant_falla_antes_de_llegar_a_la_base()
    {
        await using var db = pg.CreateDbContext(); // sin contexto de tenant
        db.Add(new UserProfile { PublicId = Guid.NewGuid(), FullName = "Huerfana" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Una_fecha_sin_zona_no_llega_a_la_base()
    {
        // Npgsql rechaza Kind=Unspecified contra timestamptz. Por eso las fechas que entran por la API se
        // normalizan en el borde (UtcDateTime); esta prueba fija el comportamiento del que eso protege.
        var tenantId = await NewTenantAsync();
        await using var db = pg.CreateDbContext(tenant: TenantScope(tenantId));
        var unspecified = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Set<UserProfile>().Where(p => p.CreatedAtUtc > unspecified).ToListAsync());
    }
}
