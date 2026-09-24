using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.IntegrationTests.Infrastructure;
using Shared.Infrastructure.Storage;
using Xunit;

namespace Api.IntegrationTests.BackgroundJobs;

/// <summary>
/// La purga de huerfanos contra el Postgres real, con la API conectada como el rol de la aplicacion (RLS activo). Lo
/// que se protege: que el registro de CADA tenant se consulte con SU contexto. Si la tarea consultara sin contexto,
/// RLS le devolveria cero filas y borraria los archivos legitimos de todos.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrphanObjectPurgeTests(PostgresFixture pg)
{
    private const string Code = OrphanObjectPurgeTask.TaskCode;
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 1, 2, 3];
    private static readonly TimeSpan Old = TimeSpan.FromDays(3);

    private static Dictionary<string, string?> Settings(long operatorTenant, bool dryRun) => new()
    {
        ["BackgroundJobs:OperatorTenantId"] = operatorTenant.ToString(),
        ["BackgroundJobs:OrphanObjects:DryRun"] = dryRun ? "true" : "false",
        ["BackgroundJobs:OrphanObjects:GraceHours"] = "24",
    };

    private static async Task<string> UploadAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "foto.png");
        var response = await client.PostAsync("/api/v1/files", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").GetProperty("objectKey").GetString()!;
    }

    private sealed record Scenario(ApiFactory Api, HttpClient Operator, string OwnedA, string OwnedB, string OldOrphan,
        string FreshOrphan, string Foreign);

    private async Task<Scenario> ArrangeAsync(bool dryRun)
    {
        var tenantA = await pg.Api.BootstrapTenantAsync();
        var tenantB = await pg.Api.BootstrapTenantAsync();
        var api = new ApiFactory(pg, Settings(tenantA.TenantId, dryRun));

        var clientA = api.CreateClient((await api.LoginAsync(tenantA.AdminEmail, tenantA.AdminPassword)).AccessToken);
        var clientB = api.CreateClient((await api.LoginAsync(tenantB.AdminEmail, tenantB.AdminPassword)).AccessToken);

        // Archivos legitimos (con fila) de DOS tenants, envejecidos: fuera del margen de gracia.
        var ownedA = await UploadAsync(clientA);
        var ownedB = await UploadAsync(clientB);
        api.Storage.Age(ownedA, Old);
        api.Storage.Age(ownedB, Old);

        // Huerfanos del tenant B: uno viejo (se borra) y uno reciente (una subida en curso: se respeta).
        var oldOrphan = $"{tenantB.TenantId}/2026/01/{Guid.NewGuid():N}.png";
        var freshOrphan = $"{tenantB.TenantId}/2026/09/{Guid.NewGuid():N}.png";
        api.Storage.Put(oldOrphan, Old);
        api.Storage.Put(freshOrphan, TimeSpan.FromMinutes(5));

        // Un objeto que no subio esta API (otro formato de clave): nunca se toca.
        var foreign = $"exports/{Guid.NewGuid():N}.csv";
        api.Storage.Put(foreign, Old);

        return new Scenario(api, clientA, ownedA, ownedB, oldOrphan, freshOrphan, foreign);
    }

    [Fact]
    public async Task Encendida_borra_solo_el_huerfano_viejo_y_respeta_lo_de_cada_tenant()
    {
        var s = await ArrangeAsync(dryRun: false);
        await using var _ = s.Api;

        var run = await s.Operator.PostAsync($"/api/v1/automated-tasks/{Code}/run");

        Assert.Equal(HttpStatusCode.OK, run.Status);
        Assert.Equal("SUCCESS", run.Data.GetProperty("status").GetString());
        Assert.Equal(1, run.Data.GetProperty("itemsProcessed").GetInt32());
        Assert.False(s.Api.Storage.Contains(s.OldOrphan));
        Assert.True(s.Api.Storage.Contains(s.OwnedA));
        Assert.True(s.Api.Storage.Contains(s.OwnedB));
        Assert.True(s.Api.Storage.Contains(s.FreshOrphan));
        Assert.True(s.Api.Storage.Contains(s.Foreign));
    }

    [Fact]
    public async Task En_seco_cuenta_pero_no_borra()
    {
        var s = await ArrangeAsync(dryRun: true);
        await using var _ = s.Api;

        var run = await s.Operator.PostAsync($"/api/v1/automated-tasks/{Code}/run");

        Assert.Equal(HttpStatusCode.OK, run.Status);
        Assert.StartsWith("En seco: se borrarian 1 ", run.Data.GetProperty("message").GetString());
        Assert.True(s.Api.Storage.Contains(s.OldOrphan));
    }

    [Fact]
    public async Task Un_archivo_dado_de_baja_conserva_su_objeto()
    {
        var s = await ArrangeAsync(dryRun: false);
        await using var _ = s.Api;
        await PostgresFixture.ExecuteAsync(pg.OwnerConnectionString,
            $"""UPDATE "StoredFile" SET "IsDeleted" = true, "DeletedAtUtc" = now() WHERE "ObjectKey" = '{s.OwnedB}'""");

        await s.Operator.PostAsync($"/api/v1/automated-tasks/{Code}/run");

        Assert.True(s.Api.Storage.Contains(s.OwnedB));
    }
}
