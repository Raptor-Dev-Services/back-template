using Authentication.Infrastructure.Persistence;
using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Context;
using Tenancy.Infrastructure.Persistence;
using Users.Infrastructure.Persistence;

namespace Host.Api.Persistence;

/// <summary>
/// Construye el <see cref="AppDbContext"/> para <c>dotnet ef</c> (migrations add / script / database update)
/// SIN arrancar la aplicacion. Vive en el Host porque es el unico proyecto que ve a todos los modulos: aqui
/// se enumeran los que aportan tablas, y un modulo que falte aqui no aparece en las migraciones.
///
/// <para>Las migraciones corren con el rol DUENO del esquema (DDL), nunca con el de la aplicacion. Por eso
/// se prefiere <c>ConnectionStrings__Migrations</c>. Sin ninguna cadena (generar una migracion no necesita
/// base viva) se usa una ficticia que nunca se abre.</para>
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>Modulos que aportan tablas. Agrega aqui el marcador de cada modulo nuevo.</summary>
    public static readonly IReadOnlyList<ModuleModel> Modules =
    [
        new(typeof(TenantConfiguration).Assembly),
        new(typeof(UserCredentialConfiguration).Assembly),
        new(typeof(UserProfileConfiguration).Assembly),
    ];

    public AppDbContext CreateDbContext(string[] args)
    {
        // Solo lo usa el tooling de `dotnet ef`, nunca la API: cargar el .env aqui no depende del entorno. NoClobber
        // respeta una variable ya fijada en el shell (asi, prefijarla inline sigue mandando).
        try { DotNetEnv.Env.TraversePath().NoClobber().Load(); } catch (FileNotFoundException) { /* sin .env */ }

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Migrations")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=backtemplate_design;Username=design;Password=design";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, new TenantContextAccessor(), NoCurrentUser.Instance, Modules);
    }
}
