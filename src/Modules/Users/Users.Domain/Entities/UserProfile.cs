using Shared.Kernel.Domain;

namespace Users.Domain.Entities;

/// <summary>
/// Perfil de un usuario dentro de su tenant. Comparte el <see cref="PublicId"/> con la credencial del
/// modulo Authentication: es el identificador que viaja en el JWT (<c>sub</c>) y en las rutas de la API.
/// </summary>
public sealed class UserProfile : TenantEntity
{
    public Guid PublicId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
