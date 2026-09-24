using System.Text.RegularExpressions;
using Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Infrastructure.Persistence;
using Users.Domain.Entities;
using Xunit;

namespace Api.IntegrationTests.Tenancy;

/// <summary>
/// La SEGUNDA barrera de aislamiento (RLS) medida sola, contra Postgres real y con el rol de la app: con
/// <c>app.tenant_id</c> fijado se ve solo lo propio, sin el no se ve nada, no se puede escribir en otro
/// tenant, y ni siquiera <c>IgnoreQueryFilters()</c> la cruza.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RlsIsolationTests(PostgresFixture pg)
{
    /// <summary>
    /// Tablas con <c>TenantId</c> que a proposito NO llevan RLS, con el motivo de la cabecera de
    /// <c>001_enable_rls.sql</c>. El mismo listado vive en el guardia del workflow de deploy.
    /// </summary>
    public static readonly string[] ExcludedOnPurpose =
    [
        "UserCredential", // el login la lee por correo antes de conocer el tenant
        "RefreshToken",   // el refresh la lee por token antes de conocer el tenant
    ];

    [Fact]
    public async Task Con_el_tenant_fijado_solo_se_ven_sus_filas()
    {
        var a = await Seed.TenantAsync(pg);
        var b = await Seed.TenantAsync(pg);
        await Seed.ProfileAsync(pg, a.Id);
        await Seed.ProfileAsync(pg, a.Id);
        await Seed.ProfileAsync(pg, b.Id);

        Assert.Equal(2, await CountProfilesAsGucAsync(a.Id.ToString()));
        Assert.Equal(1, await CountProfilesAsGucAsync(b.Id.ToString()));
    }

    [Fact]
    public async Task Sin_tenant_no_se_ve_nada_fail_closed()
    {
        var a = await Seed.TenantAsync(pg);
        await Seed.ProfileAsync(pg, a.Id);

        Assert.Equal(0, await CountProfilesAsGucAsync(null));
        Assert.Equal(0, await CountProfilesAsGucAsync(string.Empty));
    }

    [Fact]
    public async Task No_se_puede_escribir_una_fila_de_otro_tenant()
    {
        var a = await Seed.TenantAsync(pg);
        var b = await Seed.TenantAsync(pg);

        await using var connection = new NpgsqlConnection(pg.AppConnectionString);
        await connection.OpenAsync();
        await SetGucAsync(connection, a.Id.ToString());

        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "UserProfile" ("PublicId","FullName","IsActive","TenantId","CreatedAtUtc","UpdatedAtUtc","IsDeleted")
            VALUES (gen_random_uuid(), 'Intrusa', true, @other, now(), now(), false);
            """, connection);
        insert.Parameters.AddWithValue("@other", b.Id);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState); // new row violates row-level security policy
    }

    [Fact]
    public async Task IgnoreQueryFilters_cruza_la_barrera_de_EF_pero_no_la_de_RLS()
    {
        var a = await Seed.TenantAsync(pg);
        var b = await Seed.TenantAsync(pg);
        await Seed.ProfileAsync(pg, a.Id);
        await Seed.ProfileAsync(pg, b.Id);

        // Es EXACTAMENTE el descuido que RLS existe para contener: alguien quita el filtro de tenant.
        await using var db = pg.CreateDbContext(tenant: new FixedTenant(a.Id));
        var visible = await db.Set<UserProfile>()
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Select(p => p.TenantId)
            .Distinct()
            .ToListAsync();

        Assert.Equal([a.Id], visible);
    }

    [Fact]
    public async Task Una_conexion_reciclada_del_pool_no_arrastra_el_tenant_anterior()
    {
        var a = await Seed.TenantAsync(pg);
        var b = await Seed.TenantAsync(pg);
        await Seed.ProfileAsync(pg, a.Id);
        await Seed.ProfileAsync(pg, b.Id);

        // Mismo pool (misma cadena), dos "peticiones" seguidas con tenants distintos.
        await using (var first = pg.CreateDbContext(tenant: new FixedTenant(a.Id)))
            Assert.All(await first.Set<UserProfile>().IgnoreQueryFilters().ToListAsync(), p => Assert.Equal(a.Id, p.TenantId));

        await using (var second = pg.CreateDbContext(tenant: new FixedTenant(b.Id)))
            Assert.All(await second.Set<UserProfile>().IgnoreQueryFilters().ToListAsync(), p => Assert.Equal(b.Id, p.TenantId));

        await using (var anonymous = pg.CreateDbContext())
            Assert.Empty(await anonymous.Set<UserProfile>().IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Toda_tabla_con_TenantId_tiene_RLS_habilitado_y_forzado()
    {
        // Guardia contra la DERIVA del script: una entidad tenant-aware nueva con su migracion pero sin su
        // bloque en 001_enable_rls.sql queda sin segunda barrera. Esto la nombra.
        var withoutRls = await TableNamesAsync(
            """
            SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname = 'public'
              AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = c.oid AND a.attname = 'TenantId' AND NOT a.attisdropped)
              AND NOT (c.relrowsecurity AND c.relforcerowsecurity)
            ORDER BY 1;
            """);

        var unexpected = withoutRls.Except(ExcludedOnPurpose, StringComparer.Ordinal).ToArray();
        Assert.True(unexpected.Length == 0,
            $"Tablas con TenantId SIN RLS habilitado y forzado: {string.Join(", ", unexpected)}. Agrega su bloque a " +
            "Persistence/Sql/001_enable_rls.sql, o -si la exclusion es deliberada- a ExcludedOnPurpose y al guardia " +
            "del deploy, con el motivo.");
    }

    [Fact]
    public async Task Las_exclusiones_siguen_existiendo_y_siguen_sin_RLS()
    {
        // Si una exclusion desaparece del esquema o gana RLS, la lista miente: que se actualice a proposito.
        var excludedWithoutRls = await TableNamesAsync(
            """
            SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname = 'public' AND NOT c.relrowsecurity ORDER BY 1;
            """);

        Assert.All(ExcludedOnPurpose, table => Assert.Contains(table, excludedWithoutRls));
    }

    [Fact]
    public async Task El_script_quedo_aplicado_en_todas_las_tablas_que_declara()
    {
        var declared = Regex
            .Matches(PostgresFixture.ReadSql("001_enable_rls.sql"), """ALTER TABLE public\."(?<t>\w+)" ENABLE ROW LEVEL SECURITY""")
            .Select(m => m.Groups["t"].Value)
            .Distinct()
            .ToArray();
        Assert.NotEmpty(declared); // si el parseo se rompe, la prueba no puede pasar vacia

        var protectedTables = await TableNamesAsync(
            """
            SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname = 'public' AND c.relrowsecurity AND c.relforcerowsecurity;
            """);

        Assert.Empty(declared.Except(protectedTables));
    }

    [Fact]
    public async Task La_guarda_de_arranque_rechaza_un_rol_que_puede_saltarse_RLS()
    {
        Assert.Throws<InvalidOperationException>(() => RlsRoleGuard.EnsureRoleCanBeGovernedByRls("postgres", isSuperuser: true, bypassesRls: true));
        Assert.Throws<InvalidOperationException>(() => RlsRoleGuard.EnsureRoleCanBeGovernedByRls("owner", isSuperuser: false, bypassesRls: true));
        RlsRoleGuard.EnsureRoleCanBeGovernedByRls("app", isSuperuser: false, bypassesRls: false);

        // Y los roles reales quedaron como la guarda espera.
        var app = await PostgresFixture.ScalarAsync<bool>(pg.SuperuserConnectionString,
            $"SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = '{PostgresFixture.AppRole}'");
        Assert.False(app);
    }

    private async Task<long> CountProfilesAsGucAsync(string? guc)
    {
        await using var connection = new NpgsqlConnection(pg.AppConnectionString + ";Pooling=false");
        await connection.OpenAsync();
        if (guc is not null)
            await SetGucAsync(connection, guc);

        await using var count = new NpgsqlCommand("""SELECT count(*) FROM "UserProfile";""", connection);
        return (long)(await count.ExecuteScalarAsync())!;
    }

    private static async Task SetGucAsync(NpgsqlConnection connection, string tenant)
    {
        await using var set = new NpgsqlCommand("SELECT set_config('app.tenant_id', @t, false)", connection);
        set.Parameters.AddWithValue("@t", tenant);
        await set.ExecuteNonQueryAsync();
    }

    private async Task<string[]> TableNamesAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(pg.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return [.. names];
    }
}
