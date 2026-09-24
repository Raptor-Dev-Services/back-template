using Authentication.Application.UseCases.BootstrapTenant.Responses;
using Authentication.Application.UseCases.ChangePassword.Responses;
using Authentication.Application.UseCases.CompleteTwoFactorLogin.Responses;
using Authentication.Application.UseCases.TwoFactor.Responses;
using Authentication.Application.UseCases.GetMyAccount.Responses;
using Authentication.Application.UseCases.GetRoles.Responses;
using Authentication.Application.UseCases.InviteUser.Responses;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Application.UseCases.Logout.Responses;
using Authentication.Application.UseCases.RefreshSession.Responses;
using Authentication.Application.UseCases.RequestPasswordReset.Responses;
using Authentication.Application.UseCases.ResetPassword.Responses;
using Authentication.Application.UseCases.SetUserLock.Responses;
using Authentication.Presentation.Presenters;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Authentication.Presentation;

public static class ServiceCollectionEx
{
    /// <summary>Presenters registrados A MANO (uno por caso de uso) y los controllers del modulo.</summary>
    public static IServiceCollection AddAuthenticationWebApiServices(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ResultViewModel<>));

        services.AddScoped<INotificationHandler<LoginResponse>, LoginPresenter>();
        services.AddScoped<INotificationHandler<RefreshSessionResponse>, RefreshSessionPresenter>();
        services.AddScoped<INotificationHandler<LogoutResponse>, LogoutPresenter>();
        services.AddScoped<INotificationHandler<RequestPasswordResetResponse>, RequestPasswordResetPresenter>();
        services.AddScoped<INotificationHandler<ResetPasswordResponse>, ResetPasswordPresenter>();
        services.AddScoped<INotificationHandler<ChangePasswordResponse>, ChangePasswordPresenter>();
        services.AddScoped<INotificationHandler<GetMyAccountResponse>, GetMyAccountPresenter>();
        services.AddScoped<INotificationHandler<BootstrapTenantResponse>, BootstrapTenantPresenter>();
        services.AddScoped<INotificationHandler<InviteUserResponse>, InviteUserPresenter>();
        services.AddScoped<INotificationHandler<SetUserLockResponse>, SetUserLockPresenter>();
        services.AddScoped<INotificationHandler<GetRolesResponse>, GetRolesPresenter>();
        services.AddScoped<INotificationHandler<CompleteTwoFactorLoginResponse>, CompleteTwoFactorLoginPresenter>();
        services.AddScoped<INotificationHandler<BeginTwoFactorSetupResponse>, BeginTwoFactorSetupPresenter>();
        services.AddScoped<INotificationHandler<EnableTwoFactorResponse>, EnableTwoFactorPresenter>();
        services.AddScoped<INotificationHandler<DisableTwoFactorResponse>, DisableTwoFactorPresenter>();

        services.AddControllers().AddApplicationPart(typeof(ServiceCollectionEx).Assembly);
        return services;
    }
}
