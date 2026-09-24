using Shared.Kernel.Domain;

namespace Authentication.Domain.Entities;

public sealed class RefreshToken : TenantEntity
{
    public long CredentialId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}
