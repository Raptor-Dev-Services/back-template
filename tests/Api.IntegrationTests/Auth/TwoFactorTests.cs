using System.Net;
using Api.IntegrationTests.Infrastructure;
using Npgsql;
using OtpNet;
using Xunit;

namespace Api.IntegrationTests.Auth;

/// <summary>
/// 2FA TOTP de punta a punta: setup, activacion, login en dos pasos, anti-replay, codigos de recuperacion de un
/// solo uso, el reto que no sirve como access token, el secreto cifrado en la base y la desactivacion.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TwoFactorTests(PostgresFixture pg)
{
    /// <summary>
    /// El codigo del paso SIGUIENTE. La activacion consume el paso actual (anti-replay), asi que el login en el
    /// mismo periodo de 30 s necesita el siguiente, que la ventana de +-1 periodo acepta.
    /// </summary>
    private static string CodeFor(string secret, int stepsAhead) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow.AddSeconds(30 * stepsAhead));

    private async Task<(ApiClient.BootstrappedTenant Tenant, string Secret, string[] RecoveryCodes)> TenantWithTwoFactorAsync()
    {
        var tenant = await pg.Api.BootstrapTenantAsync();
        var tokens = await pg.Api.LoginAsync(tenant.AdminEmail, tenant.AdminPassword);
        var client = pg.Api.CreateClient(tokens.AccessToken);

        var setup = await client.PostAsync("/api/v1/account/2fa/setup");
        Assert.Equal(HttpStatusCode.OK, setup.Status);
        var secret = setup.Data.GetProperty("secret").GetString()!;
        Assert.StartsWith("otpauth://totp/", setup.Data.GetProperty("otpauthUri").GetString());

        var wrong = await client.PostAsync("/api/v1/account/2fa/enable", new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.Status);

        var enabled = await client.PostAsync("/api/v1/account/2fa/enable", new { code = CodeFor(secret, 0) });
        Assert.Equal(HttpStatusCode.OK, enabled.Status);
        var codes = enabled.Data.GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(10, codes.Length);

        return (tenant, secret, codes);
    }

    [Fact]
    public async Task Con_2FA_la_contrasena_solo_da_un_reto_y_el_codigo_da_la_sesion()
    {
        var (tenant, secret, _) = await TenantWithTwoFactorAsync();
        var client = pg.Api.CreateClient();

        var first = await client.PostAsync("/api/v1/auth/login", new { email = tenant.AdminEmail, password = tenant.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.True(first.Data.GetProperty("twoFactorRequired").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, first.Data.GetProperty("accessToken").ValueKind);
        var challenge = first.Data.GetProperty("challengeToken").GetString()!;

        // El reto NO es un access token: tiene audiencia propia y ningun endpoint lo acepta.
        var asBearer = await pg.Api.CreateClient(challenge).GetEnvelopeAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, asBearer.Status);

        var code = CodeFor(secret, 1);
        var second = await client.PostAsync("/api/v1/auth/login/2fa", new { challengeToken = challenge, code });
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.False(string.IsNullOrEmpty(second.Data.GetProperty("accessToken").GetString()));

        // Anti-replay: el MISMO codigo, dentro de su ventana de validez, ya no sirve.
        var replay = await client.PostAsync("/api/v1/auth/login/2fa", new { challengeToken = challenge, code });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.Status);
    }

    [Fact]
    public async Task Un_codigo_de_recuperacion_entra_una_sola_vez()
    {
        var (tenant, _, codes) = await TenantWithTwoFactorAsync();
        var client = pg.Api.CreateClient();

        async Task<ApiClient.Envelope> LoginWithCodeAsync(string code)
        {
            var first = await client.PostAsync("/api/v1/auth/login", new { email = tenant.AdminEmail, password = tenant.AdminPassword });
            return await client.PostAsync("/api/v1/auth/login/2fa",
                new { challengeToken = first.Data.GetProperty("challengeToken").GetString(), code });
        }

        // Se acepta como lo teclearia una persona: minusculas y sin guion.
        Assert.Equal(HttpStatusCode.OK, (await LoginWithCodeAsync(codes[0].ToLowerInvariant().Replace("-", ""))).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginWithCodeAsync(codes[0])).Status);
        Assert.Equal(HttpStatusCode.OK, (await LoginWithCodeAsync(codes[1])).Status);
    }

    [Fact]
    public async Task El_secreto_TOTP_se_guarda_cifrado_y_los_codigos_de_recuperacion_hasheados()
    {
        var (tenant, secret, codes) = await TenantWithTwoFactorAsync();

        await using var connection = new NpgsqlConnection(pg.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT "TotpSecretProtected" FROM "UserCredential" WHERE "Email" = @email""", connection);
        command.Parameters.AddWithValue("@email", tenant.AdminEmail);
        var stored = (string)(await command.ExecuteScalarAsync())!;
        Assert.DoesNotContain(secret, stored);

        await using var codesCommand = new NpgsqlCommand("""SELECT count(*) FROM "TwoFactorRecoveryCode" WHERE "CodeHash" = ANY(@plain)""", connection);
        codesCommand.Parameters.AddWithValue("@plain", codes);
        Assert.Equal(0L, (long)(await codesCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Desactivar_el_2FA_exige_un_segundo_factor_y_no_solo_la_sesion()
    {
        var (tenant, _, codes) = await TenantWithTwoFactorAsync();
        var client = pg.Api.CreateClient();
        var first = await client.PostAsync("/api/v1/auth/login", new { email = tenant.AdminEmail, password = tenant.AdminPassword });
        var session = await client.PostAsync("/api/v1/auth/login/2fa",
            new { challengeToken = first.Data.GetProperty("challengeToken").GetString(), code = codes[0] });
        var authed = pg.Api.CreateClient(session.Data.GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.BadRequest, (await authed.PostAsync("/api/v1/account/2fa/disable", new { code = "123456" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync("/api/v1/account/2fa/disable", new { code = codes[1] })).Status);

        // Sin 2FA el login vuelve a dar la sesion directo.
        var again = await client.PostAsync("/api/v1/auth/login", new { email = tenant.AdminEmail, password = tenant.AdminPassword });
        Assert.False(again.Data.GetProperty("twoFactorRequired").GetBoolean());
    }

    [Fact]
    public async Task Un_reto_manipulado_o_inventado_no_entra()
    {
        var result = await pg.Api.CreateClient().PostAsync("/api/v1/auth/login/2fa", new { challengeToken = "no-es-un-jwt", code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
    }
}
