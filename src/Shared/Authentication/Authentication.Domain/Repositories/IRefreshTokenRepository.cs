using Authentication.Domain.Entities;

namespace Authentication.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task InsertAsync(long tenantId, long credentialId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default);
}
