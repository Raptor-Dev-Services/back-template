using Authentication.Application.Dto;
using Authentication.Application.Sessions;
using Authentication.Application.UseCases.CompleteTwoFactorLogin.Responses;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Tenancy.Contracts.Interfaces;

namespace Authentication.Application.UseCases.CompleteTwoFactorLogin;

/// <summary>
/// Canjea el reto del login + el segundo factor por la sesion. Vuelve a comprobar TODO lo del primer paso
/// (activa, no bloqueada, tenant activo): entre un paso y otro pueden haber pasado minutos.
/// </summary>
internal sealed class CompleteTwoFactorLoginHandler(
    ITwoFactorChallenges challenges,
    IUserCredentialRepository credentials,
    SecondFactor secondFactor,
    ITenancyApi tenancy,
    SessionIssuer sessions,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteTwoFactorLoginRequest, CompleteTwoFactorLoginResponse>
{
    public const string Invalid = "El codigo no es valido o el reto expiro. Inicia sesion de nuevo.";

    public async Task<CompleteTwoFactorLoginResponse> Handle(CompleteTwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengeToken) || string.IsNullOrWhiteSpace(request.OtpCode))
            return new CompleteTwoFactorLoginInvalidFailure(Invalid);

        var publicId = await challenges.ValidateAsync(request.ChallengeToken);
        if (publicId is null)
            return new CompleteTwoFactorLoginInvalidFailure(Invalid);

        var credential = await credentials.FindForSignInAsync(publicId.Value, cancellationToken);
        if (credential is null || !credential.IsActive)
            return new CompleteTwoFactorLoginInvalidFailure(Invalid);
        if (credential.IsLocked)
            return new CompleteTwoFactorLoginForbiddenFailure("La cuenta esta bloqueada. Contacta a un administrador de tu empresa.");

        if (!await secondFactor.VerifyAsync(credential, request.OtpCode, cancellationToken))
            return new CompleteTwoFactorLoginInvalidFailure(Invalid);

        var tenant = await tenancy.GetTenantByIdAsync(credential.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return new CompleteTwoFactorLoginForbiddenFailure("La cuenta de tu empresa esta suspendida.");

        return await unitOfWork.ExecuteAsync<CompleteTwoFactorLoginResponse>(async ct =>
        {
            var (tokens, _) = await sessions.IssueAsync(credential, request.ClientIp, ct);
            credential.LastLoginAtUtc = DateTime.UtcNow;

            // Un solo guardado: la sesion nueva, el paso TOTP consumido (anti-replay) o el codigo de recuperacion
            // usado. Dos canjes simultaneos del mismo codigo: el segundo pierde en xmin (409).
            await unitOfWork.SaveChangesAsync(ct);
            return new CompleteTwoFactorLoginSuccess(LoginResultDto.From(tokens));
        }, cancellationToken);
    }
}
