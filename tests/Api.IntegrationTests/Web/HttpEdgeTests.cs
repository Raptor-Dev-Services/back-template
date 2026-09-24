using System.Net;
using Api.IntegrationTests.Infrastructure;
using Shared.Web.Http;
using Xunit;

namespace Api.IntegrationTests.Web;

/// <summary>El borde HTTP: correlacion saneada, cabeceras de seguridad, CORS por origen, limite de tasa y fail-fast.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HttpEdgeTests(PostgresFixture pg)
{
    [Fact]
    public async Task El_id_de_correlacion_del_cliente_se_sanea_y_se_devuelve()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/no-existe");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "abc-123<script>alert(1)</script>");

        using var response = await pg.Api.CreateClient().SendAsync(request);

        Assert.Equal("abc-123scriptalert1script", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Sin_id_de_correlacion_se_usa_el_de_la_traza()
    {
        using var response = await pg.Api.CreateClient().GetAsync("/api/v1/no-existe");

        var id = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Theory]
    [InlineData("/api/v1/users")]       // 401 del esquema JWT
    [InlineData("/api/v1/no-existe")]   // 404 de ruta
    public async Task Toda_respuesta_lleva_las_cabeceras_de_seguridad(string url)
    {
        using var response = await pg.Api.CreateClient().GetAsync(url);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task CORS_solo_responde_a_los_origenes_configurados_y_expone_la_correlacion()
    {
        await using var api = new ApiFactory(pg, new Dictionary<string, string?> { ["Cors:AllowedOrigins"] = "https://app.example.test, https://admin.example.test" });
        var client = api.CreateClient();

        async Task<HttpResponseMessage> PreflightAsync(string origin)
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
            preflight.Headers.Add("Origin", origin);
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            return await client.SendAsync(preflight);
        }

        using var allowed = await PreflightAsync("https://admin.example.test");
        using var denied = await PreflightAsync("https://evil.example.test");

        Assert.Equal("https://admin.example.test", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));

        using var actual = new HttpRequestMessage(HttpMethod.Get, "/api/v1/no-existe");
        actual.Headers.Add("Origin", "https://app.example.test");
        using var response = await client.SendAsync(actual);
        Assert.Contains(CorrelationIdMiddleware.HeaderName, response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    [Fact]
    public async Task El_limite_de_tasa_del_login_responde_429_con_el_envelope_y_Retry_After()
    {
        await using var api = new ApiFactory(pg, new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:AuthPerMinute"] = "3",
        });
        var client = api.CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsync("/api/v1/auth/login", new { email = "x@example.test", password = "no-es-esta" })).Status);

        using var limited = await client.PostAsync("/api/v1/auth/login",
            System.Net.Http.Json.JsonContent.Create(new { email = "x@example.test", password = "no-es-esta" }));
        var body = await limited.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("\"isSuccess\":false", body);
        Assert.True(limited.Headers.RetryAfter is not null || limited.Headers.Contains("Retry-After"));
    }

    [Theory]
    [InlineData("ForwardedHeaders:Enabled", "true")]       // sin proxies conocidos: se confiaria en cualquiera
    [InlineData("Cors:AllowedOrigins", "*")]               // comodin
    [InlineData("Jwt:Key", "corta")]                       // clave debil
    [InlineData("Totp:EncryptionKey", "")]                 // sin clave para los secretos 2FA
    [InlineData("Bootstrap:Secret", "corto")]              // secreto de bootstrap adivinable
    public void Una_configuracion_peligrosa_no_arranca(string key, string value)
    {
        using var api = new ApiFactory(pg, new Dictionary<string, string?> { [key] = value });

        Assert.ThrowsAny<Exception>(() => api.CreateClient());
    }
}
