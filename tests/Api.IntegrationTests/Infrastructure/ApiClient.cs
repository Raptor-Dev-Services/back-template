using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>Atajos para hablar con la API como lo haria un cliente real, leyendo siempre el envelope.</summary>
public static class ApiClient
{
    public sealed record Envelope(HttpStatusCode Status, JsonElement Body)
    {
        public bool IsSuccess => Body.GetProperty("isSuccess").GetBoolean();
        public string? Message => Body.GetProperty("message").GetString();
        public JsonElement Data => Body.GetProperty("data");
    }

    public static async Task<Envelope> SendAsync(this HttpClient client, HttpMethod method, string url, object? body = null, IDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            request.Headers.Add(name, value);

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(text), $"{method} {url} respondio {(int)response.StatusCode} SIN cuerpo: toda respuesta debe traer el envelope.");
        return new Envelope(response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    public static Task<Envelope> PostAsync(this HttpClient client, string url, object? body = null, IDictionary<string, string>? headers = null) =>
        client.SendAsync(HttpMethod.Post, url, body, headers);

    public static Task<Envelope> GetEnvelopeAsync(this HttpClient client, string url) => client.SendAsync(HttpMethod.Get, url);

    /// <summary>Crea un tenant nuevo con su primer administrador por el bootstrap real.</summary>
    public static async Task<BootstrappedTenant> BootstrapTenantAsync(this ApiFactory api, string? password = null)
    {
        var slug = $"t-{Guid.NewGuid():N}"[..20];
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        password ??= "Contrasena-Segura-123";

        var result = await api.CreateClient().PostAsync("/api/v1/bootstrap/tenant",
            new { tenantName = "Empresa " + slug, tenantSlug = slug, adminEmail = email, adminPassword = password, adminFullName = "Admin " + slug },
            new Dictionary<string, string> { ["X-Bootstrap-Secret"] = ApiFactory.BootstrapSecret });

        Assert.True(result.Status == HttpStatusCode.OK, $"El bootstrap respondio {(int)result.Status}: {result.Message}");
        return new BootstrappedTenant(
            result.Data.GetProperty("tenantId").GetInt64(),
            slug,
            result.Data.GetProperty("adminUserId").GetGuid(),
            email,
            password);
    }

    public static async Task<Tokens> LoginAsync(this ApiFactory api, string email, string password)
    {
        var result = await api.CreateClient().PostAsync("/api/v1/auth/login", new { email, password });
        Assert.True(result.Status == HttpStatusCode.OK, $"El login de {email} respondio {(int)result.Status}: {result.Message}");
        return new Tokens(result.Data.GetProperty("accessToken").GetString()!, result.Data.GetProperty("refreshToken").GetString()!);
    }

    public sealed record BootstrappedTenant(long TenantId, string Slug, Guid AdminUserId, string AdminEmail, string AdminPassword);

    public sealed record Tokens(string AccessToken, string RefreshToken);
}
