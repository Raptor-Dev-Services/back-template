using System.Net;
using Api.IntegrationTests.Infrastructure;
using Authentication.Infrastructure.BackgroundJobs;
using Shared.Infrastructure.BackgroundJobs;
using Shared.Kernel.BackgroundJobs;
using Xunit;

namespace Api.IntegrationTests.BackgroundJobs;

/// <summary>
/// Tareas programadas: el reclamo atomico entre replicas, el cursor que solo avanza con corridas completas, la
/// operacion reservada al tenant operador y la tarea de ejemplo (en seco por omision).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AutomatedTasksTests(PostgresFixture pg)
{
    private const string Code = ExpiredSessionPurgeTask.TaskCode;

    private static Dictionary<string, string?> OperatorSettings(long tenantId, bool dryRun = true) => new()
    {
        ["BackgroundJobs:OperatorTenantId"] = tenantId.ToString(),
        ["BackgroundJobs:SessionPurge:DryRun"] = dryRun ? "true" : "false",
    };

    [Fact]
    public async Task El_tenant_operador_lista_pausa_reanuda_ejecuta_y_ve_el_historial()
    {
        var op = await pg.Api.BootstrapTenantAsync();
        await using var api = new ApiFactory(pg, OperatorSettings(op.TenantId));
        var client = api.CreateClient((await api.LoginAsync(op.AdminEmail, op.AdminPassword)).AccessToken);

        var list = await client.GetEnvelopeAsync("/api/v1/automated-tasks");
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Contains(list.Data.EnumerateArray(), t => t.GetProperty("code").GetString() == Code);

        var paused = await client.SendAsync(HttpMethod.Put, $"/api/v1/automated-tasks/{Code}/enabled", new { isEnabled = false });
        Assert.Equal(HttpStatusCode.OK, paused.Status);
        Assert.False(paused.Data.GetProperty("isEnabled").GetBoolean());

        // "Ejecutar ahora" corre aunque este pausada: es una decision humana explicita.
        var run = await client.PostAsync($"/api/v1/automated-tasks/{Code}/run");
        Assert.Equal(HttpStatusCode.OK, run.Status);
        Assert.StartsWith("En seco", run.Data.GetProperty("message").GetString());
        Assert.Equal(op.AdminUserId, run.Data.GetProperty("triggeredByUserId").GetGuid());

        var resumed = await client.SendAsync(HttpMethod.Put, $"/api/v1/automated-tasks/{Code}/enabled", new { isEnabled = true });
        Assert.True(resumed.Data.GetProperty("isEnabled").GetBoolean());
        Assert.False(resumed.Data.GetProperty("isRunning").GetBoolean());

        var history = await client.GetEnvelopeAsync($"/api/v1/automated-tasks/{Code}/runs?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, history.Status);
        Assert.Equal(run.Data.GetProperty("id").GetInt64(), history.Data.GetProperty("items")[0].GetProperty("id").GetInt64());

        var log = await client.GetEnvelopeAsync("/api/v1/audit-log?pageSize=50");
        var actions = log.Data.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Contains("task.paused", actions);
        Assert.Contains("task.run_now", actions);
        Assert.Contains("task.resumed", actions);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/automated-tasks/no-existe/run")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetEnvelopeAsync("/api/v1/automated-tasks/no-existe/runs")).Status);
    }

    [Fact]
    public async Task El_admin_de_otro_tenant_tiene_el_permiso_pero_no_puede_operar_tareas()
    {
        var op = await pg.Api.BootstrapTenantAsync();
        var other = await pg.Api.BootstrapTenantAsync();
        await using var api = new ApiFactory(pg, OperatorSettings(op.TenantId));
        var client = api.CreateClient((await api.LoginAsync(other.AdminEmail, other.AdminPassword)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetEnvelopeAsync("/api/v1/automated-tasks")).Status);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(HttpMethod.Put, $"/api/v1/automated-tasks/{Code}/enabled", new { isEnabled = false })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/automated-tasks/{Code}/run")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetEnvelopeAsync($"/api/v1/automated-tasks/{Code}/runs")).Status);
    }

    [Fact]
    public async Task Sin_tenant_operador_configurado_nadie_opera_tareas()
    {
        var admin = await pg.Api.BootstrapTenantAsync();
        var client = pg.Api.CreateClient((await pg.Api.LoginAsync(admin.AdminEmail, admin.AdminPassword)).AccessToken);

        var result = await client.GetEnvelopeAsync("/api/v1/automated-tasks");
        Assert.Equal(HttpStatusCode.Forbidden, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task La_purga_encendida_da_de_baja_solo_lo_vencido_hace_mas_de_la_retencion()
    {
        var op = await pg.Api.BootstrapTenantAsync();
        var victim = await pg.Api.BootstrapTenantAsync();
        await pg.Api.LoginAsync(victim.AdminEmail, victim.AdminPassword);
        await pg.Api.LoginAsync(victim.AdminEmail, victim.AdminPassword);

        // Una sesion vencida hace 60 dias (fuera de la retencion) y otra vigente, del MISMO tenant.
        await PostgresFixture.ExecuteAsync(pg.OwnerConnectionString, $"""
            UPDATE "RefreshToken" SET "ExpiresAtUtc" = now() - interval '60 days'
            WHERE "Id" = (SELECT min("Id") FROM "RefreshToken" WHERE "TenantId" = {victim.TenantId});
            """);

        await using var api = new ApiFactory(pg, OperatorSettings(op.TenantId, dryRun: false));
        var client = api.CreateClient((await api.LoginAsync(op.AdminEmail, op.AdminPassword)).AccessToken);

        var run = await client.PostAsync($"/api/v1/automated-tasks/{Code}/run");
        Assert.Equal(HttpStatusCode.OK, run.Status);
        Assert.True(run.Data.GetProperty("itemsProcessed").GetInt32() >= 1);

        var deleted = await PostgresFixture.ScalarAsync<long>(pg.OwnerConnectionString,
            $"""SELECT count(*) FROM "RefreshToken" WHERE "TenantId" = {victim.TenantId} AND "IsDeleted" """);
        var alive = await PostgresFixture.ScalarAsync<long>(pg.OwnerConnectionString,
            $"""SELECT count(*) FROM "RefreshToken" WHERE "TenantId" = {victim.TenantId} AND NOT "IsDeleted" """);
        Assert.Equal(1, deleted);
        Assert.Equal(1, alive);

        // Idempotente: la segunda corrida no encuentra nada de ese tenant que marcar.
        var again = await client.PostAsync($"/api/v1/automated-tasks/{Code}/run");
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Equal(1, await PostgresFixture.ScalarAsync<long>(pg.OwnerConnectionString,
            $"""SELECT count(*) FROM "RefreshToken" WHERE "TenantId" = {victim.TenantId} AND "IsDeleted" """));
    }

    [Fact]
    public async Task El_reclamo_es_exclusivo_y_uno_huerfano_se_recupera()
    {
        var code = $"test.claim-{Guid.NewGuid():N}";
        await using (var db = pg.CreateDbContext())
            await new AutomatedTaskRepository(db).EnsureSeededAsync(code, 60, CancellationToken.None);

        var now = DateTime.UtcNow;
        await using (var first = pg.CreateDbContext())
        await using (var second = pg.CreateDbContext())
        {
            Assert.True(await new AutomatedTaskRepository(first).ClaimByCodeAsync(code, now, CancellationToken.None));
            Assert.False(await new AutomatedTaskRepository(second).ClaimByCodeAsync(code, now, CancellationToken.None));
        }

        // El proceso que la reclamo murio: pasado el timeout, otra instancia puede tomarla.
        await using (var later = pg.CreateDbContext())
            Assert.True(await new AutomatedTaskRepository(later).ClaimByCodeAsync(code, now.AddHours(7), CancellationToken.None));
    }

    [Fact]
    public async Task El_cursor_solo_avanza_con_corridas_completas()
    {
        var code = $"test.cursor-{Guid.NewGuid():N}";
        await using var db = pg.CreateDbContext();
        var repository = new AutomatedTaskRepository(db);
        await repository.EnsureSeededAsync(code, 60, CancellationToken.None);

        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow;
        await repository.RecordRunAsync(code, TaskRunResult.Partial(3, 1, "uno fallo", from, to), from, to, null, CancellationToken.None);
        Assert.Null((await repository.ListAsync()).Single(t => t.Code == code).LastCutoffUtc);

        await repository.RecordRunAsync(code, TaskRunResult.Success(4, null, from, to), from, to, null, CancellationToken.None);
        var status = (await repository.ListAsync()).Single(t => t.Code == code);
        Assert.NotNull(status.LastCutoffUtc);
        Assert.Equal("SUCCESS", status.LastRunStatus);
        Assert.False(status.IsRunning);

        var history = await repository.GetHistoryAsync(code, 1, 10);
        Assert.Equal(2, history!.TotalCount);
    }
}
