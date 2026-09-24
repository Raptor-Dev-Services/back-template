using Authentication.Application.Dto;
using Authentication.Application.UseCases.ChangePassword.Responses;
using Authentication.Domain;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;

namespace Authentication.Application.UseCases.ChangePassword;

/// <summary>
/// La prueba de identidad es la contrasena ACTUAL. Tras el cambio se revocan todas las sesiones, incluida la
/// que lo pidio: un refresh token robado deja de servir de inmediato. El cliente vuelve a iniciar sesion.
/// </summary>
internal sealed class ChangePasswordHandler(
    IUserCredentialRepository credentials,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangePasswordRequest, ChangePasswordResponse>
{
    public async Task<ChangePasswordResponse> Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.CurrentPassword))
            return new ChangePasswordValidationFailure("Escribe tu contrasena actual.");
        if (PasswordPolicy.Validate(request.NewPassword) is { } weak)
            return new ChangePasswordValidationFailure(weak);

        var credential = await credentials.FindInTenantAsync(request.UserId, cancellationToken);
        if (credential is null || !credential.CanSignIn)
            return new ChangePasswordNotFoundFailure("Cuenta no encontrada.");

        if (!hasher.Verify(request.CurrentPassword, credential.PasswordHash))
            return new ChangePasswordValidationFailure("La contrasena actual no es correcta.");
        if (hasher.Verify(request.NewPassword, credential.PasswordHash))
            return new ChangePasswordValidationFailure("La contrasena nueva debe ser distinta de la actual.");

        var nowUtc = DateTime.UtcNow;
        return await unitOfWork.ExecuteAsync<ChangePasswordResponse>(async ct =>
        {
            credential.PasswordHash = hasher.Hash(request.NewPassword);
            credential.PasswordChangedAtUtc = nowUtc;
            await refreshTokens.RevokeAllActiveAsync(credential.Id, nowUtc, RevocationReasons.PasswordChanged, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return new ChangePasswordSuccess(new AcceptedDto());
        }, cancellationToken);
    }
}
