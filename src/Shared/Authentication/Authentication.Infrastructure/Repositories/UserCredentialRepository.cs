using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Authentication.Infrastructure.Repositories;

/// <summary>
/// El login y el refresh corren ANTES de que exista contexto de tenant, asi que ignoran SOLO el filtro de
/// tenant (<see cref="QueryFilterNames.Tenant"/>) y conservan el de soft delete: una credencial borrada no
/// inicia sesion. Buscan por claves unicas en todo el sistema (correo, id), asi que no pueden devolver la
/// fila de otro tenant por accidente.
/// </summary>
internal sealed class UserCredentialRepository(AppDbContext db) : IUserCredentialRepository
{
    private IQueryable<UserCredential> AcrossTenants =>
        db.Set<UserCredential>().IgnoreQueryFilters([QueryFilterNames.Tenant]);

    public Task<UserCredential?> GetForLoginAsync(string email, CancellationToken cancellationToken = default) =>
        AcrossTenants.AsNoTracking().FirstOrDefaultAsync(e => e.Email == email, cancellationToken);

    public Task<UserCredential?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        AcrossTenants.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <summary>Incluye las borradas: el indice unico tambien las cuenta y el alta chocaria igual.</summary>
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Set<UserCredential>().IgnoreQueryFilters().AnyAsync(e => e.Email == email, cancellationToken);

    public async Task<UserCredential> InsertAsync(
        long tenantId, string email, string passwordHash, string role, CancellationToken cancellationToken = default)
    {
        var credential = new UserCredential
        {
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
        };

        db.Set<UserCredential>().Add(credential);
        await db.SaveChangesAsync(cancellationToken);
        return credential;
    }
}
