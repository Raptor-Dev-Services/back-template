using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;
using Users.Domain.Repositories;
using Users.Infrastructure.Persistence;
using Users.Infrastructure.Repositories;

namespace Users.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddUsersInfrastructureServices(this IServiceCollection services)
    {
        services.AddModuleModel<UserProfileConfiguration>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        return services;
    }
}
