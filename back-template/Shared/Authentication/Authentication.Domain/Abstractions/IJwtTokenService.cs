namespace Authentication.Domain.Abstractions;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userPublicId, string email, string role, long tenantId);
    string GenerateRefreshToken();
    DateTime GetRefreshTokenExpiry();
}
