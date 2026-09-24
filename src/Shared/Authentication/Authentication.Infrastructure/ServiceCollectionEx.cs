using Authentication.Domain.Abstractions;
using Authentication.Domain.Repositories;
using Authentication.Infrastructure.Persistence;
using Authentication.Infrastructure.Repositories;
using Authentication.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace Authentication.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddAuthenticationInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<UserCredentialConfiguration>();
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        return services;
    }
}
