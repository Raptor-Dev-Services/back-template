using Application.UseCases.Example.DisableExampleUser;
using Application.UseCases.Example.GetExampleUser;
using Application.UseCases.Example.GetExampleUsers;
using Application.UseCases.Example.InsertExampleUser;
using Application.UseCases.Example.UpdateExampleUser;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WebApi.EndPoints.Example.Presenters;

namespace WebApi;

/// <summary>
/// Extensiones de <see cref="IServiceCollection"/> para registrar los servicios de la capa WebApi.
/// </summary>
public static class ServiceCollectionEx
{
    /// <summary>
    /// Registra el <see cref="ResultViewModel{T}"/> open-generic, los presenters y los controllers.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <returns>El <see cref="IMvcBuilder"/> para configuración adicional de MVC.</returns>
    public static IMvcBuilder AddWebApiServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ResultViewModel<>));

        services.AddScoped<INotificationHandler<GetExampleUserResponse>,  GetExampleUserPresenter>();
        services.AddScoped<INotificationHandler<GetExampleUsersResponse>, GetExampleUsersPresenter>();
        services.AddScoped<INotificationHandler<InsertExampleUserResponse>, InsertExampleUserPresenter>();
        services.AddScoped<INotificationHandler<UpdateExampleUserResponse>, UpdateExampleUserPresenter>();
        services.AddScoped<INotificationHandler<DisableExampleUserResponse>, DisableExampleUserPresenter>();

        return services
            .AddControllers()
            .AddApplicationPart(Assembly.GetExecutingAssembly());
    }
}
