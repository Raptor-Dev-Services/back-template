namespace Authentication.Domain.Entities;

public sealed class UserCredential
{
    public long     Id           { get; init; }
    public Guid     PublicId     { get; init; }
    public long     TenantId     { get; init; }
    public string   Email        { get; init; } = string.Empty;
    public string   PasswordHash { get; init; } = string.Empty;
    public string   Role         { get; init; } = string.Empty;
    public bool     IsActive     { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
