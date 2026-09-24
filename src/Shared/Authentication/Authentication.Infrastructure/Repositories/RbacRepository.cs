using Authentication.Domain.Entities;
using Authentication.Domain.Rbac;
using Authentication.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Authentication.Infrastructure.Repositories;

/// <summary>
/// Roles y permisos. El tenant va EXPLICITO en cada metodo y se re-acota a mano: el login y el refresh los leen
/// antes de tener contexto, y el catalogo se re-siembra al arrancar sin peticion. Por eso estas tablas no llevan
/// RLS (ver la cabecera de 001_enable_rls.sql).
/// </summary>
internal sealed class RbacRepository(AppDbContext db) : IRbacRepository
{
    private IQueryable<Role> Roles(long tenantId) =>
        db.Set<Role>().IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(r => r.TenantId == tenantId);

    private IQueryable<UserRole> UserRoles(long tenantId) =>
        db.Set<UserRole>().IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(r => r.TenantId == tenantId);

    private IQueryable<RolePermission> RolePermissions(long tenantId) =>
        db.Set<RolePermission>().IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(r => r.TenantId == tenantId);

    public async Task SyncPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        var existing = await db.Set<Permission>().ToDictionaryAsync(p => p.Code, cancellationToken);
        foreach (var (code, name) in RbacCatalog.Permissions)
        {
            if (!existing.TryGetValue(code, out var permission))
                db.Set<Permission>().Add(new Permission { Code = code, Name = name });
            else if (permission.Name != name)
                permission.Name = name;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureTenantProvisionedAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        await SyncPermissionCatalogAsync(cancellationToken);
        var permissionIds = await db.Set<Permission>().ToDictionaryAsync(p => p.Code, p => p.Id, cancellationToken);

        var roles = await Roles(tenantId).ToDictionaryAsync(r => r.Code, cancellationToken);
        foreach (var (code, name) in RbacCatalog.Roles)
        {
            if (!roles.ContainsKey(code))
            {
                var role = new Role { TenantId = tenantId, Code = code, Name = name, IsSystem = true };
                db.Set<Role>().Add(role);
                roles[code] = role;
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        // Aditivo: agrega lo que el catalogo concede y falta; nunca quita lo que el tenant haya concedido.
        var granted = (await RolePermissions(tenantId).Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync(cancellationToken))
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        foreach (var (roleCode, permissionCodes) in RbacCatalog.RolePermissions)
        {
            var roleId = roles[roleCode].Id;
            foreach (var permissionCode in permissionCodes)
            {
                var permissionId = permissionIds[permissionCode];
                if (granted.Add((roleId, permissionId)))
                    db.Set<RolePermission>().Add(new RolePermission { TenantId = tenantId, RoleId = roleId, PermissionId = permissionId });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<long>> GetProvisionedTenantIdsAsync(CancellationToken cancellationToken = default) =>
        await db.Set<Role>().IgnoreQueryFilters([QueryFilterNames.Tenant]).Select(r => r.TenantId).Distinct().ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> GetGrantsAsync(
        long tenantId, long credentialId, CancellationToken cancellationToken = default)
    {
        var roleRows = await (
                from userRole in UserRoles(tenantId)
                join role in Roles(tenantId) on userRole.RoleId equals role.Id
                where userRole.CredentialId == credentialId
                select new { role.Id, role.Code })
            .ToListAsync(cancellationToken);

        var roleIds = roleRows.Select(r => r.Id).ToArray();
        var permissions = await (
                from rolePermission in RolePermissions(tenantId)
                join permission in db.Set<Permission>() on rolePermission.PermissionId equals permission.Id
                where roleIds.Contains(rolePermission.RoleId)
                select permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return ([.. roleRows.Select(r => r.Code).Distinct().Order()], permissions);
    }

    public async Task<IReadOnlyList<Role>> GetRolesByCodesAsync(
        long tenantId, IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default) =>
        await Roles(tenantId).Where(r => codes.Contains(r.Code)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(Role Role, IReadOnlyList<string> Permissions)>> ListRolesAsync(
        long tenantId, CancellationToken cancellationToken = default)
    {
        var roles = await Roles(tenantId).AsNoTracking().OrderBy(r => r.Code).ToListAsync(cancellationToken);
        var grants = await (
                from rolePermission in RolePermissions(tenantId)
                join permission in db.Set<Permission>() on rolePermission.PermissionId equals permission.Id
                select new { rolePermission.RoleId, permission.Code })
            .ToListAsync(cancellationToken);

        return [.. roles.Select(r => (r, (IReadOnlyList<string>)[.. grants.Where(g => g.RoleId == r.Id).Select(g => g.Code).Order()]))];
    }

    public void AssignRole(long tenantId, long credentialId, long roleId) =>
        db.Set<UserRole>().Add(new UserRole { TenantId = tenantId, CredentialId = credentialId, RoleId = roleId });
}
