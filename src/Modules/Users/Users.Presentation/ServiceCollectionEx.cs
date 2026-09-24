using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Application.UseCases.UpdateUserProfile.Responses;
using Users.Presentation.Presenters;

namespace Users.Presentation;

public static class ServiceCollectionEx
{
    /// <summary>Presenters registrados A MANO (uno por caso de uso) y los controllers del modulo.</summary>
    public static IServiceCollection AddUsersWebApiServices(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ResultViewModel<>));

        services.AddScoped<INotificationHandler<GetUserProfileResponse>, GetUserProfilePresenter>();
        services.AddScoped<INotificationHandler<GetUserProfilesResponse>, GetUserProfilesPresenter>();
        services.AddScoped<INotificationHandler<UpdateUserProfileResponse>, UpdateUserProfilePresenter>();
        services.AddScoped<INotificationHandler<DisableUserProfileResponse>, DisableUserProfilePresenter>();

        services.AddControllers().AddApplicationPart(typeof(ServiceCollectionEx).Assembly);
        return services;
    }
}
