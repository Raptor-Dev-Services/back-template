using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.IntegrationTests.Infrastructure;
using Xunit;

namespace Api.IntegrationTests.Tenancy;

/// <summary>
/// Aislamiento de punta a punta por HTTP: el tenant sale del JWT, el filtro de EF y RLS lo aplican, y un
/// usuario de un tenant no puede ni listar ni leer ni confirmar la existencia de datos de otro.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CrossTenantIsolationTests(PostgresFixture pg)
{
    [Fact]
    public async Task El_listado_solo_trae_los_usuarios_del_tenant_del_token()
    {
        var a = await Seed.TenantAsync(pg);
        var b = await Seed.TenantAsync(pg);
        var mine = await Seed.ProfileAsync(pg, a.Id, "De A");
        var theirs = await Seed.ProfileAsync(pg, b.Id, "De B");

        var client = pg.Api.CreateClient(TestJwt.Create(a.Id, permissions: ApiPermissions.UsersRead));
        var response = await client.GetAsync("/api/v1/users?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(mine.PublicId.ToString(), body);
        Assert.DoesNotContain(theirs.PublicId.ToString(), body);
    }

    [Fact]
    public async Task Leer_un_usuario_de_otro_tenant_responde_404_como_si_no_existiera()
    {
        var a = await Seed.TenantAsync(pg);
        var b = await Seed.TenantAsync(pg);
        var theirs = await Seed.ProfileAsync(pg, b.Id);

        var client = pg.Api.CreateClient(TestJwt.Create(a.Id, permissions: ApiPermissions.UsersRead));
        var foreign = await client.GetAsync($"/api/v1/users/{theirs.PublicId}");
        var invented = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Misma respuesta que para un id inventado: no se confirma que el id existe en otra empresa.
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invented.StatusCode);
        Assert.Equal(await MessageOf(invented), await MessageOf(foreign));
    }

    [Fact]
    public async Task Sin_token_no_hay_acceso_y_el_error_sale_con_el_envelope()
    {
        var response = await pg.Api.CreateClient().GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(envelope.GetProperty("isSuccess").GetBoolean());
    }

    private static async Task<string?> MessageOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();
}

/// <summary>Permisos que usan las pruebas. Se llena con el catalogo RBAC cuando exista.</summary>
internal static class ApiPermissions
{
    public static readonly string[] UsersRead = [];
}
