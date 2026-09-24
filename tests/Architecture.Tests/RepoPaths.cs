using System.Reflection;

namespace Architecture.Tests;

/// <summary>Rutas del repositorio y carga de ensamblados por nombre, compartidas por las pruebas.</summary>
internal static class RepoPaths
{
    /// <summary>La raiz del repo: el primer ancestro del directorio de salida que contiene la solucion.</summary>
    public static string Root { get; } = FindRoot();

    public static string Src => Path.Combine(Root, "src");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "back-template.slnx")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException("No se encontro back-template.slnx subiendo desde el directorio de salida.");
    }

    /// <summary>
    /// Carga por nombre. Recorrer <c>AppDomain.GetAssemblies()</c> solo ve lo que YA se cargo, y un guardia
    /// que no ve nada pasa en verde sin haber medido nada.
    /// </summary>
    public static Assembly Load(string name) => Assembly.Load(name);

    public static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
