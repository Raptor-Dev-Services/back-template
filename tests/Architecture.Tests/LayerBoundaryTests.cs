using NetArchTest.Rules;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Limites de capa y aislamiento entre modulos (regla backend-architecture), verificados sobre el IL:
///
/// <list type="bullet">
///   <item>Domain y Contracts son puros: sin EF Core, sin ASP.NET, sin mediador, sin infraestructura.</item>
///   <item>Application nunca depende de Infrastructure ni de Presentation (ni de EF Core).</item>
///   <item>Infrastructure nunca depende de Application ni de Presentation.</item>
///   <item>Presentation nunca depende de Infrastructure.</item>
///   <item>Un modulo solo ve de otro su Contracts.</item>
///   <item>Lo compartido (Kernel, Web, Infrastructure) no conoce a ningun modulo.</item>
/// </list>
///
/// Un modulo nuevo se agrega a <see cref="Modules"/>; si se olvida, <see cref="Todo_modulo_del_arbol_esta_en_el_inventario"/> lo delata.
/// </summary>
public sealed class LayerBoundaryTests
{
    /// <summary>Modulos con sus cinco capas. Authentication vive en src/Shared/ pero tiene forma de modulo.</summary>
    public static readonly string[] Modules = ["Tenancy", "Users", "Authentication"];

    private static readonly string[] Layers = ["Contracts", "Domain", "Application", "Infrastructure", "Presentation"];

    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string AspNetCore = "Microsoft.AspNetCore";
    private const string Npgsql = "Npgsql";
    private const string CommonMessaging = "Common.Messaging";
    private const string SharedInfrastructure = "Shared.Infrastructure";
    private const string SharedWeb = "Shared.Web";

    public static TheoryData<string> ModuleNames => new(Modules);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Cada_modulo_tiene_sus_cinco_capas_cargables(string module)
    {
        foreach (var layer in Layers)
        {
            var name = $"{module}.{layer}";
            var ex = Record.Exception(() => RepoPaths.Load(name));
            Assert.True(ex is null, $"No se pudo cargar '{name}': verifica el nombre del proyecto y que Architecture.Tests lo referencie.");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_es_puro(string module) =>
        AssertNoDependency($"{module}.Domain",
            EfCore, AspNetCore, Npgsql, CommonMessaging, SharedInfrastructure, SharedWeb,
            $"{module}.Application", $"{module}.Infrastructure", $"{module}.Presentation");

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contracts_es_puro(string module) =>
        AssertNoDependency($"{module}.Contracts",
            EfCore, AspNetCore, Npgsql, SharedInfrastructure, SharedWeb,
            $"{module}.Domain", $"{module}.Application", $"{module}.Infrastructure", $"{module}.Presentation");

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_no_depende_de_Infrastructure_ni_de_Presentation(string module) =>
        AssertNoDependency($"{module}.Application",
            EfCore, AspNetCore, Npgsql, SharedInfrastructure, SharedWeb,
            $"{module}.Infrastructure", $"{module}.Presentation");

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Infrastructure_no_depende_de_Application_ni_de_Presentation(string module) =>
        AssertNoDependency($"{module}.Infrastructure", $"{module}.Application", $"{module}.Presentation", SharedWeb);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Presentation_no_depende_de_Infrastructure(string module) =>
        AssertNoDependency($"{module}.Presentation", EfCore, Npgsql, SharedInfrastructure, $"{module}.Infrastructure");

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Un_modulo_solo_ve_el_Contracts_de_los_demas(string module)
    {
        var forbidden = Modules
            .Where(other => other != module)
            .SelectMany(other => new[] { $"{other}.Domain", $"{other}.Application", $"{other}.Infrastructure", $"{other}.Presentation" })
            .ToArray();

        foreach (var layer in Layers)
            AssertNoDependency($"{module}.{layer}", forbidden);
    }

    [Fact]
    public void Lo_compartido_no_conoce_a_ningun_modulo()
    {
        var anyModule = Modules.SelectMany(m => Layers.Select(l => $"{m}.{l}")).ToArray();

        AssertNoDependency("Shared.Kernel", [.. anyModule, EfCore, AspNetCore, Npgsql, SharedInfrastructure, SharedWeb]);
        AssertNoDependency("Shared.Web", [.. anyModule, EfCore, Npgsql, SharedInfrastructure]);
        AssertNoDependency("Shared.Infrastructure", [.. anyModule, SharedWeb]);
    }

    [Fact]
    public void Todo_modulo_del_arbol_esta_en_el_inventario()
    {
        // Sin esto, un modulo nuevo que nadie agrego a Modules pasaria todas las pruebas de arriba sin
        // haber sido revisado por ninguna.
        var onDisk = Directory.GetDirectories(Path.Combine(RepoPaths.Src, "Modules")).Select(Path.GetFileName);
        var missing = onDisk.Where(m => !Modules.Contains(m)).ToArray();

        Assert.True(missing.Length == 0,
            $"Modulos en src/Modules que las pruebas de arquitectura no cubren: {string.Join(", ", missing)}. " +
            "Agregalos a LayerBoundaryTests.Modules y referencia sus capas en Architecture.Tests.csproj.");
    }

    private static void AssertNoDependency(string assemblyName, params string[] forbidden)
    {
        var result = Types.InAssembly(RepoPaths.Load(assemblyName))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{assemblyName} depende de algo que su capa no puede ver ({string.Join(", ", forbidden)}). Tipos: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
