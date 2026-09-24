using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Application.UseCases.UpdateUserProfile.Responses;
using Users.Presentation.Presenters;

namespace Users.Presentation;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddUsersWebApiServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ResultViewModel<>));

        services.AddScoped<INotificationHandler<GetUserProfileResponse>,  GetUserProfilePresenter>();
        services.AddScoped<INotificationHandler<GetUserProfilesResponse>, GetUserProfilesPresenter>();
        services.AddScoped<INotificationHandler<UpdateUserProfileResponse>, UpdateUserProfilePresenter>();
        services.AddScoped<INotificationHandler<DisableUserProfileResponse>, DisableUserProfilePresenter>();

        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
        return services;
    }
}
