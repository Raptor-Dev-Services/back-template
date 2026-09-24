using Authentication.Application.Dto;
using Authentication.Application.UseCases.Logout.Responses;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Shared.Kernel.Security;

namespace Authentication.Application.UseCases.Logout;

/// <summary>Revoca el refresh token presentado. El token ES la prueba: no hace falta access token vigente.</summary>
internal sealed class LogoutHandler(IRefreshTokenRepository refreshTokens, IUnitOfWork unitOfWork)
    : IRequestHandler<LogoutRequest, LogoutResponse>
{
    public async Task<LogoutResponse> Handle(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var nowUtc = DateTime.UtcNow;
            var token = await refreshTokens.FindByHashAsync(SecureTokens.Hash(request.RefreshToken), cancellationToken);
            if (token is not null && token.IsActive(nowUtc))
            {
                token.Revoke(nowUtc, RevocationReasons.Logout);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return new LogoutSuccess(new AcceptedDto());
    }
}
