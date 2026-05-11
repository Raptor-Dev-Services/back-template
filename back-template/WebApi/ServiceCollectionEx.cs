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

public static class ServiceCollectionEx
{
    public static IMvcBuilder AddWebApiServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ResultViewModel<>));

        // Example — Presenters registered as notification handlers
        services.AddScoped<INotificationHandler<GetExampleUserResponse>, GetExampleUserPresenter>();
        services.AddScoped<INotificationHandler<GetExampleUsersResponse>, GetExampleUsersPresenter>();
        services.AddScoped<INotificationHandler<InsertExampleUserResponse>, InsertExampleUserPresenter>();
        services.AddScoped<INotificationHandler<UpdateExampleUserResponse>, UpdateExampleUserPresenter>();
        services.AddScoped<INotificationHandler<DisableExampleUserResponse>, DisableExampleUserPresenter>();

        return services
            .AddControllers()
            .AddApplicationPart(Assembly.GetExecutingAssembly());
    }
}
