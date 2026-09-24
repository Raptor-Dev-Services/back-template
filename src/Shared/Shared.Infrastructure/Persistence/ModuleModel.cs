using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Un ensamblado que aporta <c>IEntityTypeConfiguration&lt;T&gt;</c> al modelo del <see cref="AppDbContext"/>.
///
/// <para>Existe para que haya UN solo DbContext (una sola base, una sola transaccion, un solo historial de
/// migraciones) sin que este proyecto compartido referencie a ningun modulo: cada modulo es dueno de la
/// configuracion de SUS tablas en su capa Infrastructure y la publica con
/// <see cref="ModuleModelServiceCollectionEx.AddModuleModel{TMarker}"/>. El DbContext las aplica todas.</para>
/// </summary>
public sealed record ModuleModel(Assembly Assembly);

public static class ModuleModelServiceCollectionEx
{
    /// <summary>
    /// Publica las configuraciones de EF del ensamblado de <typeparamref name="TMarker"/>. Lo llama el
    /// <c>ServiceCollectionEx</c> de Infrastructure de cada modulo.
    /// </summary>
    public static IServiceCollection AddModuleModel<TMarker>(this IServiceCollection services)
    {
        services.AddSingleton(new ModuleModel(typeof(TMarker).Assembly));
        return services;
    }
}

/// <summary>
/// Clave de la cache de modelos de EF que incluye QUE modulos aportaron configuracion. Sin ella, EF
/// construye el modelo UNA vez por tipo de contexto y lo reutiliza: un proceso que arma dos contextos con
/// juegos de modulos distintos (las pruebas lo hacen) recibiria el modelo del primero para el segundo.
/// </summary>
public sealed class ModuleAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is AppDbContext app
            ? (context.GetType(), app.ModelKey, designTime)
            : (object)(context.GetType(), designTime);
}
