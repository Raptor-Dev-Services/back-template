using System.Net;
using Api.IntegrationTests.Infrastructure;
using Xunit;

namespace Api.IntegrationTests.Web;

/// <summary>
/// En produccion la API se niega a arrancar sin lo que, ausente, deja un sistema que PARECE sano: correos con
/// enlaces a localhost, un navegador que bloquea todo por CORS, invitaciones que nunca salen.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ProductionConfigurationTests(PostgresFixture pg)
{
    private static readonly Dictionary<string, string?> CompleteProduction = new()
    {
        ["environment"] = "Production",
        ["Web:BaseUrl"] = "https://app.example.test",
        ["Cors:AllowedOrigins"] = "https://app.example.test",
        ["Smtp:Host"] = "smtp.example.test",
        ["Smtp:From"] = "no-reply@example.test",
        ["ObjectStorage:AccessKey"] = "access-key",
        ["ObjectStorage:SecretKey"] = "secret-key",
    };

    [Theory]
    [InlineData("Web:BaseUrl", "")]
    [InlineData("Web:BaseUrl", "http://app.example.test")]
    [InlineData("Cors:AllowedOrigins", "")]
    [InlineData("Smtp:Host", "")]
    [InlineData("ObjectStorage:SecretKey", "")]
    public void Sin_un_obligatorio_de_produccion_no_arranca(string key, string value)
    {
        var settings = new Dictionary<string, string?>(CompleteProduction) { [key] = value };
        using var api = new ApiFactory(pg, settings);

        var ex = Assert.ThrowsAny<Exception>(() => api.CreateClient());
        Assert.Contains("produccion", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Con_la_configuracion_completa_arranca_y_sirve()
    {
        await using var api = new ApiFactory(pg, CompleteProduction);

        var response = await api.CreateClient().GetAsync("/api/v1/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
