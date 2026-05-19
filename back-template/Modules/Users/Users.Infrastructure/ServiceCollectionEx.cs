using Microsoft.Extensions.DependencyInjection;
using Users.Domain.Repositories;
using Users.Infrastructure.Repositories;

namespace Users.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddUsersInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        return services;
    }
}
