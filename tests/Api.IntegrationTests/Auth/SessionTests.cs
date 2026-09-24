using System.Net;
using Api.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Api.IntegrationTests.Auth;

/// <summary>Ciclo de vida de la sesion: login, rotacion, reuso, cierre, bloqueo, baja y cambio de contrasena.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionTests(PostgresFixture pg)
{
    [Fact]
    public async Task Correo_inexistente_y_contrasena_mala_responden_exactamente_lo_mismo()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var client = pg.Api.CreateClient();

        var wrongPassword = await client.PostAsync("/api/v1/auth/login", new { email = tenant.AdminEmail, password = "no-es-esta-contrasena" });
        var unknown = await client.PostAsync("/api/v1/auth/login", new { email = "nadie@example.test", password = "no-es-esta-contrasena" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
        Assert.Equal(wrongPassword.Message, unknown.Message);
    }

    [Fact]
    public async Task El_correo_se_normaliza_al_entrar()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        await pg.Api.LoginAsync("  " + tenant.AdminEmail.ToUpperInvariant() + " ", tenant.AdminPassword);
    }

    [Fact]
    public async Task Solo_el_hash_del_refresh_token_llega_a_la_base()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var tokens = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);

        await using var connection = new NpgsqlConnection(pg.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""SELECT count(*) FROM "RefreshToken" WHERE "TokenHash" = @raw""", connection);
        command.Parameters.AddWithValue("@raw", tokens.RefreshToken);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Refrescar_rota_el_token_y_reusar_el_viejo_cierra_todas_las_sesiones()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var first = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var client = pg.Api.CreateClient();

        var rotated = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotated.Status);
        var second = rotated.Data.GetProperty("refreshToken").GetString();
        Assert.NotEqual(first.RefreshToken, second);

        // Alguien presenta el token YA ROTADO: solo pasa si se copio. Se revocan todas las sesiones...
        var reuse = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.Status);

        // ...incluida la cadena legitima, que tiene que volver a entrar con su contrasena.
        var legit = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = second });
        Assert.Equal(HttpStatusCode.Unauthorized, legit.Status);
        await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
    }

    [Fact]
    public async Task Cerrar_sesion_revoca_el_refresh_token_y_es_idempotente()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var tokens = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var client = pg.Api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/logout", new { refreshToken = tokens.RefreshToken })).Status);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/logout", new { refreshToken = tokens.RefreshToken })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).Status);
    }

    [Fact]
    public async Task Bloquear_una_cuenta_corta_su_sesion_y_su_login()
    {
        var (tenant, admin, memberEmail, memberPassword) = await TenantWithMemberAsync();
        var member = await pg.Api.LoginAsync(memberEmail, memberPassword);
        var memberId = await MemberIdAsync(admin.AccessToken, memberEmail);

        var locked = await pg.Api.CreateClient(admin.AccessToken).PostAsync($"/api/v1/accounts/{memberId}/lock");
        Assert.Equal(HttpStatusCode.OK, locked.Status);

        var refresh = await pg.Api.CreateClient().PostAsync("/api/v1/auth/refresh", new { refreshToken = member.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.Status);

        // Con la contrasena correcta se le dice por que: ya demostro ser el titular.
        var login = await pg.Api.CreateClient().PostAsync("/api/v1/auth/login", new { email = memberEmail, password = memberPassword });
        Assert.Equal(HttpStatusCode.Forbidden, login.Status);

        // Nadie se bloquea a si mismo (dejaria al tenant sin quien lo desbloquee).
        var self = await pg.Api.CreateClient(admin.AccessToken).PostAsync($"/api/v1/accounts/{tenant.AdminUserId}/lock");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.Status);
    }

    [Fact]
    public async Task Dar_de_baja_a_un_usuario_apaga_su_credencial_en_la_misma_transaccion()
    {
        var (_, admin, memberEmail, memberPassword) = await TenantWithMemberAsync();
        var member = await pg.Api.LoginAsync(memberEmail, memberPassword);
        var memberId = await MemberIdAsync(admin.AccessToken, memberEmail);

        var disabled = await pg.Api.CreateClient(admin.AccessToken).SendAsync(HttpMethod.Delete, $"/api/v1/users/{memberId}");
        Assert.Equal(HttpStatusCode.OK, disabled.Status);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await pg.Api.CreateClient().PostAsync("/api/v1/auth/refresh", new { refreshToken = member.RefreshToken })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await pg.Api.CreateClient().PostAsync("/api/v1/auth/login", new { email = memberEmail, password = memberPassword })).Status);
    }

    [Fact]
    public async Task Restablecer_la_contrasena_cierra_las_sesiones_y_no_revela_si_la_cuenta_existe()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var tokens = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var client = pg.Api.CreateClient();

        var existing = await client.PostAsync("/api/v1/auth/password/forgot", new { email = tenant.AdminEmail });
        var unknown = await client.PostAsync("/api/v1/auth/password/forgot", new { email = "nadie@example.test" });
        Assert.Equal(HttpStatusCode.OK, existing.Status);
        Assert.Equal(existing.Status, unknown.Status);
        Assert.Equal(existing.Data.ToString(), unknown.Data.ToString());
        Assert.Equal(0, pg.Api.Outbox.CountFor("nadie@example.test"));

        var reset = await client.PostAsync("/api/v1/auth/password/reset",
            new { token = pg.Api.Outbox.LastTokenFor(tenant.AdminEmail), newPassword = "Contrasena-Nueva-987" });
        Assert.Equal(HttpStatusCode.OK, reset.Status);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/login", new { email = tenant.AdminEmail, password = tenant.AdminPassword })).Status);
        await pg.Api.LoginAsync(tenant.AdminEmail, "Contrasena-Nueva-987");
    }

    [Fact]
    public async Task Cambiar_la_contrasena_exige_la_actual_y_revoca_las_sesiones()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var tokens = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var client = pg.Api.CreateClient(tokens.AccessToken);

        var wrong = await client.PostAsync("/api/v1/account/password", new { currentPassword = "no-es-esta", newPassword = "Contrasena-Nueva-987" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.Status);

        var weak = await client.PostAsync("/api/v1/account/password", new { currentPassword = tenant.AdminPassword, newPassword = "corta" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.Status);

        var ok = await client.PostAsync("/api/v1/account/password", new { currentPassword = tenant.AdminPassword, newPassword = "Contrasena-Nueva-987" });
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await pg.Api.CreateClient().PostAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).Status);
    }

    [Fact]
    public async Task Un_administrador_no_alcanza_las_cuentas_de_otro_tenant()
    {
        var (_, adminA, _, _) = await TenantWithMemberAsync();
        var (_, adminB, memberOfB, _) = await TenantWithMemberAsync();
        var victim = await MemberIdAsync(adminB.AccessToken, memberOfB);

        var result = await pg.Api.CreateClient(adminA.AccessToken).PostAsync($"/api/v1/accounts/{victim}/lock");

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
    }

    private async Task<(ApiClient.BootstrappedTenant Tenant, ApiClient.Tokens Admin, string Email, string Password)> TenantWithMemberAsync()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var admin = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var email = $"miembro-{Guid.NewGuid():N}@example.test";
        const string password = "Contrasena-Miembro-123";

        var invited = await pg.Api.CreateClient(admin.AccessToken).PostAsync("/api/v1/accounts", new { email, fullName = "Miembro" });
        Assert.Equal(HttpStatusCode.OK, invited.Status);
        var set = await pg.Api.CreateClient().PostAsync("/api/v1/auth/password/reset",
            new { token = pg.Api.Outbox.LastTokenFor(email), newPassword = password });
        Assert.Equal(HttpStatusCode.OK, set.Status);

        return (tenant, admin, email, password);
    }

    private async Task<Guid> MemberIdAsync(string adminToken, string email)
    {
        var tokens = await pg.Api.LoginAsync(email, "Contrasena-Miembro-123");
        var me = await pg.Api.CreateClient(tokens.AccessToken).GetEnvelopeAsync("/api/v1/account/me");
        _ = adminToken;
        return me.Data.GetProperty("userId").GetGuid();
    }
}
