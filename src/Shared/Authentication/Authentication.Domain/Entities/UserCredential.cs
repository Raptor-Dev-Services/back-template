using Shared.Kernel.Domain;

namespace Authentication.Domain.Entities;

/// <summary>
/// Credencial de acceso de un usuario. El correo es unico en todo el sistema (el login no pide tenant:
/// lo deduce de la credencial).
///
/// Sin policy de RLS a proposito: el login la lee ANTES de que exista contexto de tenant. La protege el
/// filtro global de EF en toda consulta que no sea de autenticacion, y las de autenticacion re-acotan a
/// mano (ver <c>UserCredentialRepository</c>).
/// </summary>
public sealed class UserCredential : TenantEntity
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
