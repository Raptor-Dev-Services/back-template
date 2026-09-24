using Authentication.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authentication.Infrastructure.Services;

/// <summary>
/// Re-siembra el catalogo RBAC al arrancar: los permisos globales y, en cada tenant ya aprovisionado, sus roles
/// de sistema y concesiones. Aditivo e idempotente.
///
/// <para><b>Por que existe.</b> Un permiso nuevo en <c>KnownPermissions</c> solo llegaria a los tenants creados
/// DESPUES (el aprovisionamiento corre en el bootstrap). Sin esto, el endpoint que lo exige responde 403 en todos
/// los tenants existentes, sin error en ningun lado.</para>
///
/// <para>Un fallo aqui no tumba el arranque (el catalogo anterior sigue valiendo); queda como Warning y se
/// reintenta en el siguiente arranque.</para>
/// </summary>
public sealed class RbacCatalogSyncService(IServiceScopeFactory scopes, ILogger<RbacCatalogSyncService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var rbac = scope.ServiceProvider.GetRequiredService<IRbacRepository>();

            await rbac.SyncPermissionCatalogAsync(cancellationToken);
            var tenants = await rbac.GetProvisionedTenantIdsAsync(cancellationToken);
            foreach (var tenantId in tenants)
                await rbac.EnsureTenantProvisionedAsync(tenantId, cancellationToken);

            logger.LogInformation("Catalogo RBAC sincronizado en {TenantCount} tenants.", tenants.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo sincronizar el catalogo RBAC; se reintenta en el proximo arranque.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
