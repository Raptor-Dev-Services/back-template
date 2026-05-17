namespace Application.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userPublicId, string email, string role, long tenantId, long branchId);
    string GenerateRefreshToken();
    DateTime GetRefreshTokenExpiry();
}
