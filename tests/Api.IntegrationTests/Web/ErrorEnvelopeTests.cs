using System.Net;
using System.Text;
using Api.IntegrationTests.Infrastructure;
using Shared.Kernel.Security;
using Xunit;

namespace Api.IntegrationTests.Web;

/// <summary>
/// UN solo formato de error para toda la API, venga de donde venga el fallo, y nunca con detalle interno.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ErrorEnvelopeTests(PostgresFixture pg)
{
    [Fact]
    public async Task Un_error_inesperado_responde_500_generico_sin_fugar_el_detalle()
    {
        var result = await pg.Api.CreateClient().GetEnvelopeAsync("/test/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal("Ha ocurrido un error inesperado.", result.Message);
        var raw = result.Body.ToString();
        Assert.DoesNotContain("23505", raw);
        Assert.DoesNotContain("UX_UserCredential_Email", raw);
        Assert.DoesNotContain("db-interna", raw);
        Assert.DoesNotContain("envoltorio", raw);
    }

    [Fact]
    public async Task Un_cuerpo_malformado_responde_400_con_el_envelope_y_sin_el_detalle_del_parser()
    {
        using var content = new StringContent("{ \"email\": ", Encoding.UTF8, "application/json");
        using var response = await pg.Api.CreateClient().PostAsync("/api/v1/auth/login", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"isSuccess\":false", body);
        Assert.DoesNotContain("LineNumber", body);
        Assert.DoesNotContain("BytePosition", body);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Una_ruta_inexistente_responde_404_con_el_envelope()
    {
        var result = await pg.Api.CreateClient().GetEnvelopeAsync("/api/v1/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Un_token_sin_el_permiso_responde_403_con_el_envelope()
    {
        var tenant = await Seed.TenantAsync(pg);
        var client = pg.Api.CreateClient(TestJwt.Create(tenant.Id, permissions: [KnownPermissions.UsersRead]));

        var result = await client.SendAsync(HttpMethod.Delete, $"/api/v1/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Un_token_firmado_con_otra_clave_no_entra()
    {
        var forged = TestJwt.Create(1, permissions: [KnownPermissions.UsersManage]).Split('.');
        var tampered = $"{forged[0]}.{forged[1]}.{new string('A', forged[2].Length)}";

        var result = await pg.Api.CreateClient(tampered).GetEnvelopeAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
    }
}
