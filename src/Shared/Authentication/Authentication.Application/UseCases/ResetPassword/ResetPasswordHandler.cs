using Authentication.Application.Dto;
using Authentication.Application.UseCases.ResetPassword.Responses;
using Authentication.Domain;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Shared.Kernel.Security;

namespace Authentication.Application.UseCases.ResetPassword;

/// <summary>
/// Consume el token y fija la contrasena. Revoca TODAS las sesiones: quien restablece no tiene sesion propia que
/// conservar, y si el disparador fue alguien con un refresh token robado, esto lo corta en el acto.
/// </summary>
internal sealed class ResetPasswordHandler(
    IPasswordSetupTokenRepository setupTokens,
    IUserCredentialRepository credentials,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork) : IRequestHandler<ResetPasswordRequest, ResetPasswordResponse>
{
    public const string InvalidToken = "El enlace no es valido o ya expiro. Pide uno nuevo.";

    public async Task<ResetPasswordResponse> Handle(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (PasswordPolicy.Validate(request.NewPassword) is { } weak)
            return new ResetPasswordWeakPasswordFailure(weak);
        if (string.IsNullOrWhiteSpace(request.Token))
            return new ResetPasswordInvalidTokenFailure(InvalidToken);

        var nowUtc = DateTime.UtcNow;
        var token = await setupTokens.FindByHashAsync(SecureTokens.Hash(request.Token), cancellationToken);
        if (token is null || !token.IsUsable(nowUtc))
            return new ResetPasswordInvalidTokenFailure(InvalidToken);

        var credential = await credentials.FindForSignInAsync(token.CredentialId, cancellationToken);
        if (credential is null || !credential.IsActive)
            return new ResetPasswordInvalidTokenFailure(InvalidToken);

        return await unitOfWork.ExecuteAsync<ResetPasswordResponse>(async ct =>
        {
            credential.PasswordHash = hasher.Hash(request.NewPassword);
            credential.PasswordChangedAtUtc = nowUtc;
            token.Consume(nowUtc);
            await refreshTokens.RevokeAllActiveAsync(credential.Id, nowUtc, RevocationReasons.PasswordChanged, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return new ResetPasswordSuccess(new AcceptedDto());
        }, cancellationToken);
    }
}
