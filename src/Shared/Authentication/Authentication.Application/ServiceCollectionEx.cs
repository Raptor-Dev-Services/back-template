using Authentication.Application.UseCases.Login;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Application.UseCases.RefreshToken;
using Authentication.Application.UseCases.RefreshToken.Responses;
using Authentication.Application.UseCases.Register;
using Authentication.Application.UseCases.Register.Responses;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Authentication.Application;

public static class ServiceCollectionEx
{
    /// <summary>
    /// Registra los handlers del modulo, uno por uno. Sin escaneo de ensamblados a proposito (regla
    /// backend-architecture): un handler que falta aqui lo delata la prueba de composicion del Host, no
    /// un 500 en produccion.
    /// </summary>
    public static IServiceCollection AddAuthenticationApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IRequestHandler<LoginRequest, LoginResponse>, LoginHandler>();
        services.AddScoped<IRequestHandler<RegisterRequest, RegisterResponse>, RegisterHandler>();
        services.AddScoped<IRequestHandler<RefreshTokenRequest, RefreshTokenResponse>, RefreshTokenHandler>();
        return services;
    }
}
