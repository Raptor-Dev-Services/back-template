using Authentication.Application.UseCases.BootstrapTenant.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.BootstrapTenant;

/// <summary>
/// Crea un tenant (si no existe) y su PRIMER administrador. Sin sesion, pero gateado por el secreto
/// <c>Bootstrap:Secret</c>, que llega en la cabecera <c>X-Bootstrap-Secret</c>.
/// </summary>
public sealed record BootstrapTenantRequest(
    string? Secret,
    string TenantName,
    string TenantSlug,
    string AdminEmail,
    string AdminPassword,
    string AdminFullName) : IRequest<BootstrapTenantResponse>;
