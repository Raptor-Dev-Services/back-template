using Application.UseCases.Auth.Login.Responses;
using Application.UseCases.Auth.RefreshToken.Responses;
using Application.UseCases.Users.CreateUser.Responses;
using Application.UseCases.Users.DisableUser.Responses;
using Application.UseCases.Users.GetUser.Responses;
using Application.UseCases.Users.GetUsers.Responses;
using Application.UseCases.Users.UpdateUser.Responses;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WebApi.EndPoints.Auth.Presenters;
using WebApi.EndPoints.Users.Presenters;

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

        // Auth presenters
        services.AddScoped<INotificationHandler<LoginResponse>,        LoginPresenter>();
        services.AddScoped<INotificationHandler<RefreshTokenResponse>, RefreshTokenPresenter>();

        // Users presenters
        services.AddScoped<INotificationHandler<GetUserResponse>,     GetUserPresenter>();
        services.AddScoped<INotificationHandler<GetUsersResponse>,    GetUsersPresenter>();
        services.AddScoped<INotificationHandler<CreateUserResponse>,  CreateUserPresenter>();
        services.AddScoped<INotificationHandler<UpdateUserResponse>,  UpdateUserPresenter>();
        services.AddScoped<INotificationHandler<DisableUserResponse>, DisableUserPresenter>();

        return services
            .AddControllers()
            .AddApplicationPart(Assembly.GetExecutingAssembly());
    }
}
