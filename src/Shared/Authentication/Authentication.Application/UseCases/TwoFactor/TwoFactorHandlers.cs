using Authentication.Application.Dto;
using Authentication.Application.Sessions;
using Authentication.Application.UseCases.TwoFactor.Responses;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Audit;
using Shared.Kernel.Context;

namespace Authentication.Application.UseCases.TwoFactor;

internal sealed class BeginTwoFactorSetupHandler(
    IUserCredentialRepository credentials,
    ITotpService totp,
    ISecretProtector protector,
    IUnitOfWork unitOfWork) : IRequestHandler<BeginTwoFactorSetupRequest, BeginTwoFactorSetupResponse>
{
    public async Task<BeginTwoFactorSetupResponse> Handle(BeginTwoFactorSetupRequest request, CancellationToken cancellationToken)
    {
        var credential = await credentials.FindInTenantAsync(request.UserId, cancellationToken);
        if (credential is null || !credential.CanSignIn)
            return new BeginTwoFactorSetupNotFoundFailure("Cuenta no encontrada.");
        if (credential.IsTwoFactorEnabled)
            return new BeginTwoFactorSetupConflictFailure("El 2FA ya esta activo. Desactivalo antes de configurarlo de nuevo.");

        // Pendiente hasta que un codigo lo confirme: un setup abandonado no deja la cuenta con 2FA a medias.
        var secret = totp.GenerateSecret();
        credential.TotpSecretProtected = protector.Protect(secret);
        credential.LastTotpStep = 0;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BeginTwoFactorSetupSuccess(new TwoFactorSetupDto(secret, totp.BuildOtpauthUri(secret, credential.Email)));
    }
}

internal sealed class EnableTwoFactorHandler(
    IUserCredentialRepository credentials,
    ITwoFactorRecoveryCodeRepository codes,
    ITotpService totp,
    ISecretProtector protector,
    IRecoveryCodes recoveryCodes,
    TotpOptions options,
    IUnitOfWork unitOfWork) : IRequestHandler<EnableTwoFactorRequest, EnableTwoFactorResponse>
{
    public async Task<EnableTwoFactorResponse> Handle(EnableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var credential = await credentials.FindInTenantAsync(request.UserId, cancellationToken);
        if (credential is null || !credential.CanSignIn)
            return new EnableTwoFactorValidationFailure("Cuenta no encontrada.");
        if (credential.IsTwoFactorEnabled)
            return new EnableTwoFactorConflictFailure("El 2FA ya esta activo.");
        if (string.IsNullOrWhiteSpace(credential.TotpSecretProtected))
            return new EnableTwoFactorValidationFailure("Primero genera el codigo QR (configurar 2FA).");

        var secret = protector.TryUnprotect(credential.TotpSecretProtected);
        var step = secret is null ? null : totp.VerifyStep(secret, request.OtpCode ?? string.Empty, minStepExclusive: 0);
        if (step is null)
            return new EnableTwoFactorValidationFailure("El codigo no coincide. Revisa la hora del telefono e intenta de nuevo.");

        var plain = recoveryCodes.Generate(options.RecoveryCodeCount);
        return await unitOfWork.ExecuteAsync<EnableTwoFactorResponse>(async ct =>
        {
            var nowUtc = DateTime.UtcNow;
            credential.TotpEnabledAtUtc = nowUtc;
            credential.LastTotpStep = step.Value;

            await codes.ConsumeAllAsync(credential.Id, nowUtc, ct);
            foreach (var code in plain)
                codes.Add(new TwoFactorRecoveryCode { TenantId = credential.TenantId, CredentialId = credential.Id, CodeHash = recoveryCodes.Hash(code) });

            await unitOfWork.SaveChangesAsync(ct);
            return new EnableTwoFactorSuccess(new RecoveryCodesDto(plain));
        }, cancellationToken);
    }
}

internal sealed class DisableTwoFactorHandler(
    IUserCredentialRepository credentials,
    ITwoFactorRecoveryCodeRepository codes,
    SecondFactor secondFactor,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IRequestHandler<DisableTwoFactorRequest, DisableTwoFactorResponse>
{
    public async Task<DisableTwoFactorResponse> Handle(DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var credential = await credentials.FindInTenantAsync(request.UserId, cancellationToken);
        if (credential is null || !credential.IsTwoFactorEnabled)
            return new DisableTwoFactorValidationFailure("El 2FA no esta activo.");

        // Una sesion robada no basta para quitar el segundo factor: hace falta el segundo factor.
        if (!await secondFactor.VerifyAsync(credential, request.OtpCode, cancellationToken))
            return new DisableTwoFactorValidationFailure("El codigo no es valido.");

        return await unitOfWork.ExecuteAsync<DisableTwoFactorResponse>(async ct =>
        {
            var nowUtc = DateTime.UtcNow;
            credential.TotpEnabledAtUtc = null;
            credential.TotpSecretProtected = null;
            credential.LastTotpStep = 0;
            await codes.ConsumeAllAsync(credential.Id, nowUtc, ct);
            audit.Append("user.2fa_disabled", "UserCredential", credential.PublicId.ToString(), "Segundo factor desactivado por su titular.");
            await unitOfWork.SaveChangesAsync(ct);
            return new DisableTwoFactorSuccess(new AcceptedDto());
        }, cancellationToken);
    }
}
