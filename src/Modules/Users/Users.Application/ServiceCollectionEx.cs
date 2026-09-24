using Authentication.Contracts.Events;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Users.Application.IntegrationEventHandlers;
using Users.Application.UseCases.DisableUserProfile;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Application.UseCases.GetUserProfile;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Application.UseCases.GetUserProfiles;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Application.UseCases.UpdateUserProfile;
using Users.Application.UseCases.UpdateUserProfile.Responses;

namespace Users.Application;

public static class ServiceCollectionEx
{
    /// <summary>Registra handlers de casos de uso y de eventos de integracion del modulo, uno por uno.</summary>
    public static IServiceCollection AddUsersApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IRequestHandler<GetUserProfileRequest, GetUserProfileResponse>, GetUserProfileHandler>();
        services.AddScoped<IRequestHandler<GetUserProfilesRequest, GetUserProfilesResponse>, GetUserProfilesHandler>();
        services.AddScoped<IRequestHandler<UpdateUserProfileRequest, UpdateUserProfileResponse>, UpdateUserProfileHandler>();
        services.AddScoped<IRequestHandler<DisableUserProfileRequest, DisableUserProfileResponse>, DisableUserProfileHandler>();

        services.AddScoped<INotificationHandler<UserShouldBeCreatedIntegrationEvent>, UserShouldBeCreatedHandler>();
        return services;
    }
}
