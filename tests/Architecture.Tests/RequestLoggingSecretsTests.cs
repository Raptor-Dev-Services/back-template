using System.Text.Json;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// El query string NO debe llegar al log: puede llevar tokens (un enlace de restablecimiento, un access_token de
/// WebSocket) o datos personales.
///
/// <para>El log por peticion propio (<c>UseSerilogRequestLogging</c>) ya deja fuera la query. Pero ASP.NET Core
/// trae el suyo, <c>Microsoft.AspNetCore.Hosting.Diagnostics</c>, que emite "Request starting/finished" con la URL
/// COMPLETA. Lo calla el override de <c>Microsoft.AspNetCore</c> a Warning; si alguien lo sube a Information para
/// depurar enrutado, reabre la fuga. Estas pruebas lo impiden en todos los appsettings versionados.</para>
/// </summary>
public sealed class RequestLoggingSecretsTests
{
    private const string HostingLogger = "Microsoft.AspNetCore.Hosting.Diagnostics";

    public static TheoryData<string> Files => new(
        Directory.GetFiles(Path.Combine(RepoPaths.Src, "Host", "Host.Api"), "appsettings*.json").Select(Path.GetFileName)!);

    [Theory]
    [MemberData(nameof(Files))]
    public void Ningun_appsettings_deja_que_el_framework_registre_la_url_completa(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Src, "Host", "Host.Api", file)));
        if (!doc.RootElement.TryGetProperty("Serilog", out var serilog)
            || !serilog.TryGetProperty("MinimumLevel", out var level)
            || !level.TryGetProperty("Override", out var overrides))
            return; // no toca los overrides: hereda los de appsettings.json

        foreach (var name in new[] { "Microsoft.AspNetCore", HostingLogger })
        {
            if (overrides.TryGetProperty(name, out var value))
                Assert.True(value.GetString() is "Warning" or "Error" or "Fatal",
                    $"{file} baja '{name}' a '{value.GetString()}': el logger de hosting escribiria la URL con su query string.");
        }
    }

    /// <summary>Ensamblados con casos de uso. Se cargan por NOMBRE: recorrer lo ya cargado pasaria en vacio.</summary>
    private static readonly string[] ApplicationAssemblies =
    [
        "Authentication.Application",
        "Tenancy.Application",
        "Users.Application",
    ];

    /// <summary>Fragmentos de nombre que huelen a secreto. Mas amplia que la de Common a proposito: es la red.</summary>
    private static readonly string[] SuspiciousTerms = ["code", "password", "token", "secret", "otp", "pin"];

    /// <summary>Propiedades con nombre sospechoso que NO son secretas, con el motivo. Agregar aqui es una decision.</summary>
    private static readonly Dictionary<string, string> NotSecret = new(StringComparer.Ordinal)
    {
        ["RunAutomatedTaskNowRequest.Code"] = "codigo publico de una tarea programada",
        ["SetAutomatedTaskEnabledRequest.Code"] = "codigo publico de una tarea programada",
        ["GetAutomatedTaskHistoryRequest.Code"] = "codigo publico de una tarea programada",
        ["InviteUserRequest.RoleCodes"] = "codigos de rol del catalogo RBAC",
        ["RoleDto.Code"] = "codigo de rol del catalogo RBAC",
    };

    /// <summary>
    /// El pipeline de Common registra CADA request y CADA respuesta (nivel Information) y tapa los campos por NOMBRE,
    /// con una lista negra. Un campo secreto con un nombre fuera de esa lista se escribe en claro, sin error: asi
    /// llegaron al log los codigos TOTP (request, propiedad <c>Code</c>) y los diez de recuperacion (respuesta,
    /// <c>Codes</c>). Esta prueba recorre todos los tipos publicos de los casos de uso -requests, respuestas y sus
    /// DTOs-: todo nombre sospechoso lo tiene que tapar Common, o estar en <see cref="NotSecret"/> con su motivo.
    /// </summary>
    [Fact]
    public void Common_tapa_en_el_log_todo_campo_de_request_o_respuesta_con_pinta_de_secreto()
    {
        var requestTypes = ApplicationAssemblies
            .Select(System.Reflection.Assembly.Load)
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && (t.IsPublic || t.IsNestedPublic)
                && !t.Name.Contains('<'))
            .ToList();
        Assert.Contains(requestTypes, t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Common.Messaging.IRequest<>)));

        var leaks = requestTypes
            .SelectMany(t => t.GetProperties().Select(p => (Key: $"{t.Name}.{p.Name}", p.Name)))
            .Where(x => SuspiciousTerms.Any(term => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Where(x => !Common.Messaging.SensitiveDataMasker.EsSensible(x.Name) && !NotSecret.ContainsKey(x.Key))
            .Select(x => x.Key)
            .ToList();

        Assert.True(leaks.Count == 0,
            "Estos campos de request o respuesta tienen nombre de secreto y Common los registraria EN CLARO: " + string.Join(", ", leaks)
            + ". Renombralos con un termino que Common tape (Otp..., ...Password, ...Token, ...Secret) o, si no son secretos, "
            + "agregalos a NotSecret con su motivo.");
    }

    [Fact]
    public void La_configuracion_base_silencia_explicitamente_el_logger_de_hosting()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Src, "Host", "Host.Api", "appsettings.json")));
        var overrides = doc.RootElement.GetProperty("Serilog").GetProperty("MinimumLevel").GetProperty("Override");

        Assert.Equal("Warning", overrides.GetProperty("Microsoft.AspNetCore").GetString());
        Assert.Equal("Warning", overrides.GetProperty(HostingLogger).GetString());
    }
}
