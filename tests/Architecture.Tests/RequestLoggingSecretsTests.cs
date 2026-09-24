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

    [Fact]
    public void La_configuracion_base_silencia_explicitamente_el_logger_de_hosting()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Src, "Host", "Host.Api", "appsettings.json")));
        var overrides = doc.RootElement.GetProperty("Serilog").GetProperty("MinimumLevel").GetProperty("Override");

        Assert.Equal("Warning", overrides.GetProperty("Microsoft.AspNetCore").GetString());
        Assert.Equal("Warning", overrides.GetProperty(HostingLogger).GetString());
    }
}
