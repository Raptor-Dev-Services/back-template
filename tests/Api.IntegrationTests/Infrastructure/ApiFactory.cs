using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>
/// La API real (Program.cs completo: middlewares, filtros, auth, guardas de arranque) contra el Postgres de
/// <see cref="PostgresFixture"/>, conectada con el ROL DE LA APLICACION. No se sustituye ningun servicio:
/// lo que se prueba es lo que se despliega.
/// </summary>
public sealed class ApiFactory(PostgresFixture pg, IDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", pg.AppConnectionString);
        builder.UseSetting("Jwt:Key", TestJwt.Key);
        builder.UseSetting("Jwt:Issuer", TestJwt.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwt.Audience);

        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            builder.UseSetting(key, value);
    }

    /// <summary>Cliente con el JWT indicado en el header Authorization.</summary>
    public HttpClient CreateClient(string bearerToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }
}
