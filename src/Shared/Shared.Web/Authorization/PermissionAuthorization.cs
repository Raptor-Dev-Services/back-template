using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Shared.Kernel.Security;

namespace Shared.Web.Authorization;

/// <summary>
/// Convencion de policies RBAC: <c>perm:&lt;code&gt;</c> exige un claim <c>permission</c> con ese codigo. En un
/// endpoint: <c>[Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.UsersManage)]</c> (concatenacion
/// de constantes, valida en un atributo).
/// </summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string For(string permissionCode) => Prefix + permissionCode;
}

/// <summary>Exige que el usuario porte el permiso <see cref="Permission"/>.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Concede si el usuario autenticado trae un claim <c>permission</c> con el codigo exigido.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(AppClaimTypes.Permission, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Crea al vuelo la policy de cada <c>perm:&lt;code&gt;</c> (sin registrarlas una por una). Cualquier otra se
/// delega al proveedor por defecto.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionPolicy.Prefix.Length..]))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
