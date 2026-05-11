namespace Domain.Entities.Example;

public sealed class ExampleUser
{
    public long     Id           { get; init; }
    public Guid     PublicId     { get; init; }
    public string   FullName     { get; init; } = string.Empty;
    public string   Email        { get; init; } = string.Empty;
    public string   Department   { get; init; } = string.Empty;
    public string   Notes        { get; init; } = string.Empty;
    public bool     IsActive     { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
