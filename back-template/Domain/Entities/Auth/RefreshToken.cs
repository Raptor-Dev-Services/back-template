namespace Domain.Entities.Auth;

public sealed class RefreshToken
{
    public long     Id           { get; init; }
    public long     UserId       { get; init; }
    public string   Token        { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public bool     IsRevoked    { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
