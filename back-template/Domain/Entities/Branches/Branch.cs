namespace Domain.Entities.Branches;

public sealed class Branch
{
    public long     Id           { get; init; }
    public Guid     PublicId     { get; init; }
    public long     TenantId     { get; init; }
    public string   Name         { get; init; } = string.Empty;
    public bool     IsActive     { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
