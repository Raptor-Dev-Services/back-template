namespace Tenancy.Contracts.Dtos;

public sealed record TenantDto(Guid PublicId, string Name, string Slug, bool IsActive);
