using System.Net;
using System.Text.Json;
using Api.IntegrationTests.Infrastructure;
using Host.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Api.IntegrationTests.Web;

[Collection(PostgresCollection.Name)]
public sealed class HealthTests(PostgresFixture pg)
{
    [Fact]
    public async Task Live_responde_sin_consultar_dependencias()
    {
        using var response = await pg.Api.CreateClient().GetAsync("/health/live");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task Ready_comprueba_Postgres_con_el_rol_de_la_aplicacion()
    {
        using var response = await pg.Api.CreateClient().GetAsync("/health/ready");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var postgres = body.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "postgres");
        Assert.Equal("Healthy", postgres.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Un_check_caido_no_expone_el_detalle_de_la_excepcion()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new(HealthStatus.Unhealthy, "PostgreSQL no responde.",
                TimeSpan.FromMilliseconds(3), new InvalidOperationException("Host=db-interna;Username=backtemplate_app"), null),
        };
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthResponseWriter.WriteAsync(context, new HealthReport(entries, TimeSpan.FromMilliseconds(3)));

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Unhealthy", text);
        Assert.DoesNotContain("db-interna", text);
        Assert.DoesNotContain("backtemplate_app", text);
        Assert.DoesNotContain("no responde", text);
    }
}
