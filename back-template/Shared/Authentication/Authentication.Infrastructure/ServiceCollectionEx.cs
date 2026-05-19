using Authentication.Application.Services;
using Authentication.Domain.Repositories;
using Authentication.Infrastructure.Repositories;
using Authentication.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Authentication.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddAuthenticationInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IRefreshTokenRepository,   RefreshTokenRepository>();
        services.AddScoped<IJwtTokenService,           JwtTokenService>();
        services.AddScoped<IPasswordHasher,            PasswordHasher>();
        return services;
    }
}
