using System.Net;
using Api.IntegrationTests.Infrastructure;
using Xunit;

namespace Api.IntegrationTests.Audit;

/// <summary>Las acciones sensibles dejan una linea en la bitacora del tenant, con su actor, y nadie ve la ajena.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditLogTests(PostgresFixture pg)
{
    [Fact]
    public async Task Invitar_bloquear_y_dar_de_baja_quedan_en_la_bitacora_con_su_actor()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var admin = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var client = pg.Api.CreateClient(admin.AccessToken);

        var invited = await client.PostAsync("/api/v1/accounts", new { email = $"m-{Guid.NewGuid():N}@example.test", fullName = "Miembro" });
        var memberId = invited.Data.GetProperty("userId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/accounts/{memberId}/lock")).Status);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(HttpMethod.Delete, $"/api/v1/users/{memberId}")).Status);

        var log = await client.GetEnvelopeAsync("/api/v1/audit-log?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, log.Status);
        var entries = log.Data.GetProperty("items").EnumerateArray().ToList();
        var actions = entries.Select(e => e.GetProperty("action").GetString()).ToList();

        Assert.Contains("tenant.bootstrapped", actions);
        Assert.Contains("user.invited", actions);
        Assert.Contains("user.locked", actions);
        Assert.Contains("user.disabled", actions);
        Assert.All(entries.Where(e => e.GetProperty("action").GetString() != "tenant.bootstrapped"),
            e => Assert.Equal(tenant.AdminUserId, e.GetProperty("actorUserId").GetGuid()));

        var filtered = await client.GetEnvelopeAsync("/api/v1/audit-log?action=user.locked");
        Assert.Equal(1, filtered.Data.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task La_bitacora_de_un_tenant_no_se_ve_desde_otro()
    {
        var a = await pg.Api.BootstrapTenantAsync();
        var b = await pg.Api.BootstrapTenantAsync();
        var adminB = await pg.Api.LoginAsync(b.AdminEmail, b.AdminPassword);

        var log = await pg.Api.CreateClient(adminB.AccessToken).GetEnvelopeAsync("/api/v1/audit-log?pageSize=100");
        var summaries = log.Data.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("summary").GetString()!).ToList();

        // Solo su propio bootstrap: nada que mencione al administrador del otro tenant.
        Assert.Single(summaries);
        Assert.Contains(b.AdminUserId.ToString(), summaries[0]);
        Assert.DoesNotContain(summaries, s => s.Contains(a.AdminUserId.ToString()));
    }
}
