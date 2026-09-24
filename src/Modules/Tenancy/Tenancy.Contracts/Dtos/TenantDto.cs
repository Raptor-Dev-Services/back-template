namespace Tenancy.Contracts.Dtos;

/// <summary>Vista publica de un tenant para otros modulos. <see cref="Id"/> es el que viaja en el claim tenant_id.</summary>
public sealed record TenantDto(long Id, Guid PublicId, string Name, string Slug, bool IsActive);
