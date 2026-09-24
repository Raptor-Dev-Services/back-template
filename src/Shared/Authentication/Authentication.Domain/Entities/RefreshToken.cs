namespace Authentication.Domain.Entities;

public sealed class RefreshToken
{
    public long     Id           { get; init; }
    public long     CredentialId { get; init; }
    public string   Token        { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public bool     IsRevoked    { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
