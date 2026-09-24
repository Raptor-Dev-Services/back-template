using System.Text.RegularExpressions;
using Xunit;

namespace Api.IntegrationTests.Tenancy;

/// <summary>
/// La lista de tablas SIN RLS a proposito vive en tres sitios: estas pruebas, el encabezado de 001_enable_rls.sql y
/// el guardia del deploy. Si el deploy exime una tabla que las pruebas no, produccion aceptaria en silencio una
/// tabla sin aislamiento que aqui fallaria. Esta prueba ata el deploy a la lista probada.
/// </summary>
public sealed partial class RlsExclusionDriftTests
{
    [Fact]
    public void El_guardia_del_deploy_exime_exactamente_las_mismas_tablas_que_las_pruebas()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRoot(), ".github", "workflows", "deploy.yml"));
        var match = ExclusionLine().Match(workflow);
        Assert.True(match.Success, "No se encontro SIN_RLS_A_PROPOSITO en deploy.yml.");

        var inDeploy = Quoted().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToArray();
        Assert.Equal(RlsIsolationTests.ExcludedOnPurpose.Order().ToArray(), inDeploy);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "back-template.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro back-template.slnx.");
    }

    [GeneratedRegex("SIN_RLS_A_PROPOSITO=\"([^\"]+)\"")]
    private static partial Regex ExclusionLine();

    [GeneratedRegex("'([A-Za-z]+)'")]
    private static partial Regex Quoted();
}
