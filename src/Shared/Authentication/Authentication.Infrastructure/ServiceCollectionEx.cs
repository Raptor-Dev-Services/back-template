using Authentication.Domain.Abstractions;
using Authentication.Domain.Repositories;
using Authentication.Infrastructure.BackgroundJobs;
using Authentication.Infrastructure.Persistence;
using Authentication.Infrastructure.Repositories;
using Authentication.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.BackgroundJobs;

namespace Authentication.Infrastructure;

public static class ServiceCollectionEx
{
    /// <summary>
    /// Repositorios, hasher, emisor de tokens y la re-siembra del catalogo RBAC. <see cref="AuthOptions"/> lo
    /// registra el Host (se enlaza de la configuracion y se valida al arrancar).
    /// </summary>
    public static IServiceCollection AddAuthenticationInfrastructureServices(this IServiceCollection services)
    {
        services.AddModuleModel<UserCredentialConfiguration>();

        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordSetupTokenRepository, PasswordSetupTokenRepository>();
        services.AddScoped<IRbacRepository, RbacRepository>();
        services.AddScoped<ITwoFactorRecoveryCodeRepository, TwoFactorRecoveryCodeRepository>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();

        // 2FA: TotpOptions lo registra el Host (se valida la clave de cifrado al arrancar).
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddSingleton<IRecoveryCodes, RecoveryCodes>();
        services.AddSingleton<ITwoFactorChallenges, TwoFactorChallenges>();

        // Tarea programada de ejemplo (SessionPurgeOptions lo registra el Host desde BackgroundJobs:SessionPurge).
        services.AddScoped<IAutomatedTask, ExpiredSessionPurgeTask>();

        services.AddHostedService<RbacCatalogSyncService>();
        return services;
    }
}
