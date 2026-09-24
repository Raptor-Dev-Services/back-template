using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.IntegrationTests.Infrastructure;
using Api.IntegrationTests.Tenancy;
using Xunit;

namespace Api.IntegrationTests.Files;

/// <summary>
/// Archivos: la subida valida tipo y firma de bytes y registra al dueno; leer exige ser del tenant dueno (404 si no,
/// sin confirmar que la clave existe), y el lote solo devuelve las claves propias.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FilesTests(PostgresFixture pg)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 1, 2, 3];

    private static async Task<ApiClient.Envelope> UploadAsync(HttpClient client, byte[] content, string contentType, string fileName = "foto.png")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        var response = await client.PostAsync("/api/v1/files", form);
        var body = await response.Content.ReadAsStringAsync();
        return new ApiClient.Envelope(response.StatusCode, JsonDocument.Parse(body).RootElement.Clone());
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        return pg.Api.CreateClient((await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword)).AccessToken);
    }

    [Fact]
    public async Task Subir_y_pedir_la_url_de_un_archivo_propio()
    {
        var client = await AdminClientAsync();

        var uploaded = await UploadAsync(client, Png, "image/png", "..\\..\\evil<script>.png");
        Assert.Equal(HttpStatusCode.OK, uploaded.Status);
        var key = uploaded.Data.GetProperty("objectKey").GetString()!;
        Assert.EndsWith(".png", key);
        Assert.True(pg.Api.Storage.Contains(key));
        Assert.Equal(Png.Length, uploaded.Data.GetProperty("sizeBytes").GetInt64());

        var url = await client.GetEnvelopeAsync($"/api/v1/files/{key}");
        Assert.Equal(HttpStatusCode.OK, url.Status);
        Assert.Contains(key, url.Data.GetProperty("url").GetString());
    }

    [Fact]
    public async Task La_clave_de_otro_tenant_responde_404_y_no_sale_en_el_lote()
    {
        var owner = await AdminClientAsync();
        var stranger = await AdminClientAsync();
        var key = (await UploadAsync(owner, Png, "image/png")).Data.GetProperty("objectKey").GetString()!;
        var mine = (await UploadAsync(stranger, Png, "image/png")).Data.GetProperty("objectKey").GetString()!;

        var single = await stranger.GetEnvelopeAsync($"/api/v1/files/{key}");
        Assert.Equal(HttpStatusCode.NotFound, single.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetEnvelopeAsync("/api/v1/files/1/2026/01/no-existe.png")).Status);

        var batch = await stranger.PostAsync("/api/v1/files/urls", new { objectKeys = new[] { key, mine, "a/../b.png" } });
        Assert.Equal(HttpStatusCode.OK, batch.Status);
        var keys = batch.Data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("objectKey").GetString()).ToList();
        Assert.Equal([mine], keys);
    }

    [Fact]
    public async Task Un_contenido_que_no_coincide_con_el_tipo_declarado_se_rechaza()
    {
        var client = await AdminClientAsync();

        var spoofed = await UploadAsync(client, "<html><script>alert(1)</script>"u8.ToArray(), "image/png");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, spoofed.Status);
        Assert.False(spoofed.IsSuccess);

        var disallowed = await UploadAsync(client, "hola"u8.ToArray(), "text/plain", "nota.txt");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, disallowed.Status);
    }

    [Fact]
    public async Task Sin_permiso_de_archivos_no_se_sube_ni_se_lee()
    {
        var tenant = await Seed.TenantAsync(pg);
        var client = pg.Api.CreateClient(TestJwt.Create(tenant.Id, permissions: ApiPermissions.UsersRead));

        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(client, Png, "image/png")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetEnvelopeAsync("/api/v1/files/1/2026/01/x.png")).Status);
    }

    [Fact]
    public async Task Mas_de_50_claves_en_el_lote_es_422()
    {
        var client = await AdminClientAsync();
        var keys = Enumerable.Range(0, 51).Select(i => $"1/2026/01/{i}.png").ToArray();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync("/api/v1/files/urls", new { objectKeys = keys })).Status);
    }
}
