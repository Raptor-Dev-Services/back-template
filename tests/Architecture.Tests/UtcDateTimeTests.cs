using System.Text.RegularExpressions;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Vigila la ley UTC (regla utc-datetime): el backend trabaja SIEMPRE en UTC y jamas usa la hora local del
/// servidor. El dia que alguien escriba <c>DateTime.Now</c>, el build y las pruebas siguen verdes, y el
/// defecto aparece cuando el servidor de produccion corre en otra zona que la maquina de quien lo escribio:
/// una fecha guardada con el offset equivocado, sin ningun error.
///
/// Escanea el CODIGO FUENTE, no el IL: <c>DateTime.Now</c> es una llamada a propiedad estatica que
/// NetArchTest (que razona sobre dependencias entre tipos) no distingue de <c>DateTime.UtcNow</c>.
/// </summary>
public sealed class UtcDateTimeTests
{
    private static readonly (Regex Forbidden, string Instead)[] ForbiddenForms =
    [
        (new Regex(@"\bDateTime\.Now\b"), "DateTime.UtcNow"),
        (new Regex(@"\bDateTime\.Today\b"), "DateTime.UtcNow.Date"),
        (new Regex(@"\bDateTimeOffset\.Now\b"), "DateTimeOffset.UtcNow"),
        (new Regex(@"\bTimeZoneInfo\.Local\b"), "el offset explicito del caso de uso (la conversion a local es del frontend)"),
        (new Regex(@"\.ToLocalTime\(\)"), "mantener UTC; la conversion a local es del frontend"),
    ];

    [Fact]
    public void El_backend_nunca_usa_la_hora_local_del_servidor()
    {
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(RepoPaths.Src, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var code = line.Split("//")[0]; // los comentarios pueden NOMBRAR lo prohibido para explicarlo

                foreach (var (forbidden, instead) in ForbiddenForms)
                {
                    if (forbidden.IsMatch(code))
                        violations.Add($"{Path.GetRelativePath(RepoPaths.Root, file)}:{i + 1}  usa '{forbidden}', usa {instead}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "El backend usa la hora local del servidor:\n  " + string.Join("\n  ", violations));
    }

    [Fact]
    public void El_escaneo_ve_el_codigo_fuente()
    {
        // Guardia del guardia: si la ruta cambia y no encuentra archivos, la prueba de arriba pasaria vacia.
        var count = Directory.EnumerateFiles(RepoPaths.Src, "*.cs", SearchOption.AllDirectories).Count(f => !IsBuildOutput(f));
        Assert.True(count > 50, $"Solo se vieron {count} archivos .cs en src/: el escaneo no esta viendo el codigo.");
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");
}
