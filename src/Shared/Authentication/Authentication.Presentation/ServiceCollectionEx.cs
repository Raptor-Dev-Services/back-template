using Authentication.Application.UseCases.Login.Responses;
using Authentication.Application.UseCases.RefreshToken.Responses;
using Authentication.Application.UseCases.Register.Responses;
using Authentication.Presentation.Presenters;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Authentication.Presentation;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddAuthenticationWebApiServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ResultViewModel<>));

        services.AddScoped<INotificationHandler<LoginResponse>,        LoginPresenter>();
        services.AddScoped<INotificationHandler<RegisterResponse>,     RegisterPresenter>();
        services.AddScoped<INotificationHandler<RefreshTokenResponse>, RefreshTokenPresenter>();

        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
        return services;
    }
}
