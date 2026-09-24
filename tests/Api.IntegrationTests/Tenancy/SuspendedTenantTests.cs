using System.Net;
using Api.IntegrationTests.Infrastructure;
using Shared.Web.Tenancy;
using Tenancy.Domain.Entities;
using Xunit;

namespace Api.IntegrationTests.Tenancy;

/// <summary>
/// Un access token emitido ANTES de suspender al tenant sigue siendo criptograficamente valido hasta que vence; el
/// guard lo corta en la siguiente peticion con 403 y el envelope de siempre.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SuspendedTenantTests(PostgresFixture pg)
{
    [Fact]
    public async Task Un_token_vigente_de_un_tenant_suspendido_recibe_403()
    {
        var suspended = await Seed.TenantAsync(pg, TenantStatus.Suspended);
        var client = pg.Api.CreateClient(TestJwt.Create(suspended.Id, permissions: ApiPermissions.UsersRead));

        var result = await client.GetEnvelopeAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(TenantStatusGuardMiddleware.SuspendedMessage, result.Message);
    }

    [Fact]
    public async Task Suspender_despues_del_login_corta_la_sesion_y_el_refresh()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var tokens = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);

        await PostgresFixture.ExecuteAsync(pg.OwnerConnectionString,
            $"""UPDATE "Tenant" SET "Status" = '{TenantStatus.Suspended}' WHERE "Id" = {tenant.TenantId}""");

        var me = await pg.Api.CreateClient(tokens.AccessToken).GetEnvelopeAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.Forbidden, me.Status);

        var refresh = await pg.Api.CreateClient().PostAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.False(refresh.IsSuccess);
    }

    [Fact]
    public async Task Las_peticiones_anonimas_no_pasan_por_el_guard()
    {
        var health = await pg.Api.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
