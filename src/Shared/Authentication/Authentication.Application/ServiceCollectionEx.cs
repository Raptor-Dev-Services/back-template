using Authentication.Application.IntegrationEventHandlers;
using Authentication.Application.Sessions;
using Authentication.Application.UseCases.BootstrapTenant;
using Authentication.Application.UseCases.BootstrapTenant.Responses;
using Authentication.Application.UseCases.ChangePassword;
using Authentication.Application.UseCases.CompleteTwoFactorLogin;
using Authentication.Application.UseCases.CompleteTwoFactorLogin.Responses;
using Authentication.Application.UseCases.TwoFactor;
using Authentication.Application.UseCases.TwoFactor.Responses;
using Authentication.Application.UseCases.ChangePassword.Responses;
using Authentication.Application.UseCases.GetMyAccount;
using Authentication.Application.UseCases.GetMyAccount.Responses;
using Authentication.Application.UseCases.GetRoles;
using Authentication.Application.UseCases.GetRoles.Responses;
using Authentication.Application.UseCases.InviteUser;
using Authentication.Application.UseCases.InviteUser.Responses;
using Authentication.Application.UseCases.Login;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Application.UseCases.Logout;
using Authentication.Application.UseCases.Logout.Responses;
using Authentication.Application.UseCases.RefreshSession;
using Authentication.Application.UseCases.RefreshSession.Responses;
using Authentication.Application.UseCases.RequestPasswordReset;
using Authentication.Application.UseCases.RequestPasswordReset.Responses;
using Authentication.Application.UseCases.ResetPassword;
using Authentication.Application.UseCases.ResetPassword.Responses;
using Authentication.Application.UseCases.SetUserLock;
using Authentication.Application.UseCases.SetUserLock.Responses;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Users.Contracts.Events;

namespace Authentication.Application;

public static class ServiceCollectionEx
{
    /// <summary>
    /// Registra los handlers del modulo uno por uno. Sin escaneo de ensamblados a proposito (regla
    /// backend-architecture): un handler que falte aqui lo delata la prueba de composicion, no un 500 en produccion.
    /// </summary>
    public static IServiceCollection AddAuthenticationApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<SessionIssuer>();
        services.AddScoped<PasswordSetupMailer>();
        services.AddScoped<SecondFactor>();

        services.AddScoped<IRequestHandler<LoginRequest, LoginResponse>, LoginHandler>();
        services.AddScoped<IRequestHandler<RefreshSessionRequest, RefreshSessionResponse>, RefreshSessionHandler>();
        services.AddScoped<IRequestHandler<LogoutRequest, LogoutResponse>, LogoutHandler>();
        services.AddScoped<IRequestHandler<RequestPasswordResetRequest, RequestPasswordResetResponse>, RequestPasswordResetHandler>();
        services.AddScoped<IRequestHandler<ResetPasswordRequest, ResetPasswordResponse>, ResetPasswordHandler>();
        services.AddScoped<IRequestHandler<ChangePasswordRequest, ChangePasswordResponse>, ChangePasswordHandler>();
        services.AddScoped<IRequestHandler<BootstrapTenantRequest, BootstrapTenantResponse>, BootstrapTenantHandler>();
        services.AddScoped<IRequestHandler<InviteUserRequest, InviteUserResponse>, InviteUserHandler>();
        services.AddScoped<IRequestHandler<SetUserLockRequest, SetUserLockResponse>, SetUserLockHandler>();
        services.AddScoped<IRequestHandler<GetRolesRequest, GetRolesResponse>, GetRolesHandler>();
        services.AddScoped<IRequestHandler<GetMyAccountRequest, GetMyAccountResponse>, GetMyAccountHandler>();
        services.AddScoped<IRequestHandler<CompleteTwoFactorLoginRequest, CompleteTwoFactorLoginResponse>, CompleteTwoFactorLoginHandler>();
        services.AddScoped<IRequestHandler<BeginTwoFactorSetupRequest, BeginTwoFactorSetupResponse>, BeginTwoFactorSetupHandler>();
        services.AddScoped<IRequestHandler<EnableTwoFactorRequest, EnableTwoFactorResponse>, EnableTwoFactorHandler>();
        services.AddScoped<IRequestHandler<DisableTwoFactorRequest, DisableTwoFactorResponse>, DisableTwoFactorHandler>();

        services.AddScoped<INotificationHandler<UserDisabledIntegrationEvent>, UserDisabledHandler>();
        return services;
    }
}
