using Authentication.Application.Dto;
using Authentication.Application.Sessions;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Domain;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Tenancy.Contracts.Interfaces;

namespace Authentication.Application.UseCases.Login;

internal sealed class LoginHandler(
    IUserCredentialRepository credentials,
    IPasswordHasher hasher,
    ITenancyApi tenancy,
    SessionIssuer sessions,
    ITwoFactorChallenges challenges,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginRequest, LoginResponse>
{
    public const string InvalidCredentials = "Correo o contrasena incorrectos.";

    public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = Identity.NormalizeEmail(request.Email);
        var credential = email.Length == 0 ? null : await credentials.FindForSignInAsync(email, cancellationToken);

        // Se verifica SIEMPRE, exista o no la cuenta (contra un hash ficticio si no existe): responder mas
        // rapido para un correo inexistente le diria a un atacante que cuentas hay.
        var passwordOk = hasher.Verify(request.Password ?? string.Empty, credential?.PasswordHash);
        if (credential is null || !passwordOk || !credential.IsActive)
            return new LoginInvalidCredentialsFailure(InvalidCredentials);

        if (credential.IsLocked)
            return new LoginForbiddenFailure("La cuenta esta bloqueada. Contacta a un administrador de tu empresa.");

        var tenant = await tenancy.GetTenantByIdAsync(credential.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return new LoginForbiddenFailure("La cuenta de tu empresa esta suspendida.");

        // Con 2FA la contrasena es solo el primer paso: no se emite sesion, se emite el reto del segundo.
        if (credential.IsTwoFactorEnabled)
            return new LoginSuccess(LoginResultDto.Challenge(challenges.Issue(credential.PublicId)));

        var (tokens, _) = await sessions.IssueAsync(credential, request.ClientIp, cancellationToken);
        credential.LastLoginAtUtc = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginSuccess(LoginResultDto.From(tokens));
    }
}
