namespace Domain.Entities.Tenants;

public sealed class Tenant
{
    public long     Id           { get; init; }
    public Guid     PublicId     { get; init; }
    public string   Name         { get; init; } = string.Empty;
    public string   Slug         { get; init; } = string.Empty;
    public bool     IsActive     { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
