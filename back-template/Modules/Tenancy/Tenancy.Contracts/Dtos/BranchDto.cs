namespace Tenancy.Contracts.Dtos;

public sealed record BranchDto(Guid PublicId, string Name, long TenantId, bool IsActive);
