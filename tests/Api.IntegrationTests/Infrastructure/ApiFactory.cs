using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Email;
using Shared.Kernel.Storage;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>
/// La API real (Program.cs completo: middlewares, filtros, auth, guardas de arranque) contra el Postgres de
/// <see cref="PostgresFixture"/>, conectada con el ROL DE LA APLICACION. Solo se sustituye el correo saliente,
/// por un buzon en memoria del que las pruebas leen los enlaces con token, y el almacenamiento de objetos, por uno
/// en memoria (el registro de propiedad SI es el real, en Postgres).
/// </summary>
public sealed class ApiFactory(PostgresFixture pg, IDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    public const string BootstrapSecret = "integration-tests-bootstrap-secret-0123456789abcdef";

    public CapturingEmailSender Outbox { get; } = new();

    public InMemoryObjectStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", pg.AppConnectionString);
        builder.UseSetting("Jwt:Key", TestJwt.Key);
        builder.UseSetting("Jwt:Issuer", TestJwt.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwt.Audience);
        builder.UseSetting("Bootstrap:Secret", BootstrapSecret);
        builder.UseSetting("Totp:EncryptionKey", "integration-tests-totp-encryption-key-0123456789");
        builder.UseSetting("Web:BaseUrl", "https://app.example.test");
        // El despachador en segundo plano competiria con las pruebas por las mismas filas: se ejercita con "ejecutar ahora".
        builder.UseSetting("BackgroundJobs:DispatcherEnabled", "false");

        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailSender>(Outbox);
            services.AddSingleton<IObjectStorage>(Storage);
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
        });
    }

    /// <summary>Cliente con el JWT indicado en el header Authorization.</summary>
    public HttpClient CreateClient(string bearerToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }
}

/// <summary>Buzon en memoria: lo que la API "envio", para leer el enlace con su token.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<(string To, string Subject, string Body)> _sent = new();

    public IReadOnlyCollection<(string To, string Subject, string Body)> Sent => [.. _sent];

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }

    /// <summary>El token del ultimo enlace enviado a <paramref name="email"/>.</summary>
    public string LastTokenFor(string email)
    {
        var message = _sent.LastOrDefault(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));
        if (message.Body is null)
            throw new InvalidOperationException($"No se envio ningun correo a {email}.");

        var match = TokenInLink().Match(message.Body);
        return match.Success
            ? Uri.UnescapeDataString(System.Net.WebUtility.HtmlDecode(match.Groups[1].Value))
            : throw new InvalidOperationException($"El correo a {email} no trae un enlace con token.");
    }

    public int CountFor(string email) => _sent.Count(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("token=([^\"&<\\s]+)")]
    private static partial Regex TokenInLink();
}
