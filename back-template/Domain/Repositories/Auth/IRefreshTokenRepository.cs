using Domain.Entities.Auth;

namespace Domain.Repositories.Auth;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task InsertAsync(long userId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeAllByUserIdAsync(long userId, CancellationToken cancellationToken = default);
}
