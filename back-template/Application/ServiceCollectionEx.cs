using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

/// <summary>
/// Extensiones de <see cref="IServiceCollection"/> para registrar los servicios de la capa Application.
/// </summary>
public static class ServiceCollectionEx
{
    /// <summary>
    /// Registra el Mediator y todos los handlers del ensamblado Application.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <returns>La misma <paramref name="services"/> para encadenamiento.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediator(Assembly.GetExecutingAssembly());
        return services;
    }
}
