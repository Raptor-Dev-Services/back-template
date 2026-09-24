using Authentication.Application.Dto;
using Authentication.Domain.Abstractions;
using Authentication.Application.UseCases.Register.Responses;
using Authentication.Contracts.Events;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Tenancy.Contracts.Interfaces;

namespace Authentication.Application.UseCases.Register;

internal sealed class RegisterHandler : IRequestHandler<RegisterRequest, RegisterResponse>
{
    private readonly IUserCredentialRepository _credentials;
    private readonly IRefreshTokenRepository   _refreshTokens;
    private readonly IPasswordHasher           _hasher;
    private readonly IJwtTokenService          _jwt;
    private readonly ITenancyApi               _tenancy;
    private readonly IMediator                 _mediator;

    public RegisterHandler(
        IUserCredentialRepository credentials,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        ITenancyApi tenancy,
        IMediator mediator)
    {
        _credentials   = credentials;
        _refreshTokens = refreshTokens;
        _hasher        = hasher;
        _jwt           = jwt;
        _tenancy       = tenancy;
        _mediator      = mediator;
    }

    public async Task<RegisterResponse> Handle(RegisterRequest request, CancellationToken cancellationToken)
    {
        var tenant = await _tenancy.GetTenantByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return new RegisterTenantNotFoundFailure("Empresa no encontrada o inactiva.");

        var exists = await _credentials.ExistsByEmailAsync(request.Email, cancellationToken);
        if (exists)
            return new RegisterEmailConflictFailure("El correo ya está registrado.");

        var hash       = _hasher.Hash(request.Password);
        var credential = await _credentials.InsertAsync(
            request.TenantId, request.Email, hash, request.Role,
            cancellationToken);

        await _mediator.Publish(new UserShouldBeCreatedIntegrationEvent(
            credential.PublicId,
            credential.TenantId,
            request.FullName,
            credential.Email,
            credential.Role), cancellationToken);

        var accessToken  = _jwt.GenerateAccessToken(credential.PublicId, credential.Email, credential.Role, credential.TenantId);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiry       = _jwt.GetRefreshTokenExpiry();

        await _refreshTokens.InsertAsync(credential.Id, refreshToken, expiry, cancellationToken);

        return new RegisterSuccess(new TokenDto(accessToken, refreshToken, expiry));
    }
}
