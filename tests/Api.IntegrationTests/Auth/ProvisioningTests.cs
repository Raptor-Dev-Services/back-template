using System.Net;
using Api.IntegrationTests.Infrastructure;
using Xunit;

namespace Api.IntegrationTests.Auth;

/// <summary>
/// El alta de usuarios YA NO es anonima. El antiguo <c>POST /api/auth/register</c> tomaba el TenantId y el Role
/// del cuerpo: cualquiera se daba de alta como Admin en la empresa que quisiera. Ahora el primer administrador
/// sale del bootstrap gateado por secreto, y los demas los invita un administrador.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ProvisioningTests(PostgresFixture pg)
{
    private static readonly Dictionary<string, string> Secret = new() { ["X-Bootstrap-Secret"] = ApiFactory.BootstrapSecret };

    private static object BootstrapBody(string slug, string email) => new
    {
        tenantName = "Empresa",
        tenantSlug = slug,
        adminEmail = email,
        adminPassword = "Contrasena-Segura-123",
        adminFullName = "Admin",
    };

    [Fact]
    public async Task El_registro_anonimo_ya_no_existe()
    {
        var result = await pg.Api.CreateClient().PostAsync("/api/v1/auth/register",
            new { email = "x@example.test", password = "Contrasena-Segura-123", tenantId = 1, fullName = "X", role = "Admin" });

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Sin_secreto_o_con_uno_equivocado_el_bootstrap_se_rechaza()
    {
        var client = pg.Api.CreateClient();

        var withoutHeader = await client.PostAsync("/api/v1/bootstrap/tenant", BootstrapBody("sin-secreto", "a@example.test"));
        var wrong = await client.PostAsync("/api/v1/bootstrap/tenant", BootstrapBody("secreto-malo", "b@example.test"),
            new Dictionary<string, string> { ["X-Bootstrap-Secret"] = ApiFactory.BootstrapSecret + "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, withoutHeader.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.Status);
    }

    [Fact]
    public async Task Sin_secreto_configurado_el_bootstrap_esta_apagado()
    {
        await using var api = new ApiFactory(pg, new Dictionary<string, string?> { ["Bootstrap:Secret"] = "" });

        var result = await api.CreateClient().PostAsync("/api/v1/bootstrap/tenant", BootstrapBody("apagado", "c@example.test"), Secret);

        Assert.Equal(HttpStatusCode.Forbidden, result.Status);
    }

    [Fact]
    public async Task El_bootstrap_crea_tenant_y_administrador_que_entra_con_sus_permisos()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var tokens = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);

        var me = await pg.Api.CreateClient(tokens.AccessToken).GetEnvelopeAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.OK, me.Status);
        Assert.Equal(tenant.TenantId, me.Data.GetProperty("tenantId").GetInt64());
        Assert.False(me.Data.GetProperty("twoFactorEnabled").GetBoolean());
        var permissions = me.Data.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToArray();
        Assert.Contains("users.manage", permissions);
        Assert.Contains("users.read", permissions);

        // El perfil lo creo el modulo Users, en la misma transaccion y con el tenant correcto (tabla con RLS).
        var users = await pg.Api.CreateClient(tokens.AccessToken).GetEnvelopeAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.OK, users.Status);
        Assert.Equal(1, users.Data.GetProperty("totalCount").GetInt32());
        Assert.Equal(tenant.AdminUserId, users.Data.GetProperty("items")[0].GetProperty("publicId").GetGuid());
    }

    [Fact]
    public async Task El_bootstrap_no_mete_un_segundo_administrador_en_un_tenant_con_usuarios()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();

        var again = await pg.Api.CreateClient().PostAsync("/api/v1/bootstrap/tenant",
            BootstrapBody(tenant.Slug, $"intruso-{Guid.NewGuid():N}@example.test"), Secret);

        Assert.Equal(HttpStatusCode.Conflict, again.Status);
        await Assert.ThrowsAnyAsync<Exception>(() => pg.Api.LoginAsync("intruso@example.test", "Contrasena-Segura-123"));
    }

    [Fact]
    public async Task Un_administrador_invita_y_el_invitado_fija_su_contrasena_con_el_enlace()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var admin = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var email = $"miembro-{Guid.NewGuid():N}@example.test";

        var invited = await pg.Api.CreateClient(admin.AccessToken).PostAsync("/api/v1/accounts",
            new { email, fullName = "Miembro", roleCodes = new[] { "Member" } });
        Assert.Equal(HttpStatusCode.OK, invited.Status);

        // Sin contrasena utilizable hasta que use el enlace.
        var before = await pg.Api.CreateClient().PostAsync("/api/v1/auth/login", new { email, password = "Contrasena-Segura-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, before.Status);

        var token = pg.Api.Outbox.LastTokenFor(email);
        var set = await pg.Api.CreateClient().PostAsync("/api/v1/auth/password/reset", new { token, newPassword = "Otra-Contrasena-456" });
        Assert.Equal(HttpStatusCode.OK, set.Status);

        var member = await pg.Api.LoginAsync(email, "Otra-Contrasena-456");

        // El token es de un solo uso.
        var reuse = await pg.Api.CreateClient().PostAsync("/api/v1/auth/password/reset", new { token, newPassword = "Tercera-Contrasena-789" });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.Status);

        // Un miembro no administra usuarios.
        var escalation = await pg.Api.CreateClient(member.AccessToken).PostAsync("/api/v1/accounts",
            new { email = $"otro-{Guid.NewGuid():N}@example.test", fullName = "Otro", roleCodes = new[] { "Admin" } });
        Assert.Equal(HttpStatusCode.Forbidden, escalation.Status);
    }

    [Fact]
    public async Task Un_rol_inexistente_no_se_concede()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var admin = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);

        var result = await pg.Api.CreateClient(admin.AccessToken).PostAsync("/api/v1/accounts",
            new { email = $"x-{Guid.NewGuid():N}@example.test", fullName = "X", roleCodes = new[] { "SuperAdmin" } });

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
    }
}
