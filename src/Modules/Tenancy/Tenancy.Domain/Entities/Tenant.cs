using Shared.Kernel.Domain;

namespace Tenancy.Domain.Entities;

/// <summary>Ciclo de vida de un tenant. Un tenant suspendido no puede operar (lo corta el guard de peticion).</summary>
public enum TenantStatus
{
    Active,
    Suspended,
}

/// <summary>
/// El cliente del SaaS (una empresa). Entidad GLOBAL: es la raiz de la tenencia, asi que no pertenece a
/// ningun tenant ni lleva policy de RLS; la leen el login y el bootstrap antes de que exista contexto.
/// </summary>
public sealed class Tenant : GlobalEntity
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Identificador legible y unico (minusculas, digitos y guiones). Se normaliza al guardar.</summary>
    public string Slug { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public bool IsActive => Status == TenantStatus.Active && !IsDeleted;
}
