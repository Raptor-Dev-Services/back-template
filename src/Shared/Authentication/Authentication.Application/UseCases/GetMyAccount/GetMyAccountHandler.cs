using Authentication.Application.Dto;
using Authentication.Application.UseCases.GetMyAccount.Responses;
using Authentication.Domain.Repositories;
using Common.Messaging;

namespace Authentication.Application.UseCases.GetMyAccount;

internal sealed class GetMyAccountHandler(IUserCredentialRepository credentials, IRbacRepository rbac)
    : IRequestHandler<GetMyAccountRequest, GetMyAccountResponse>
{
    public async Task<GetMyAccountResponse> Handle(GetMyAccountRequest request, CancellationToken cancellationToken)
    {
        var credential = await credentials.FindInTenantAsync(request.UserId, cancellationToken);
        if (credential is null)
            return new GetMyAccountNotFoundFailure("Cuenta no encontrada.");

        var (roles, permissions) = await rbac.GetGrantsAsync(credential.TenantId, credential.Id, cancellationToken);
        return new GetMyAccountSuccess(new AccountDto(
            credential.PublicId, credential.TenantId, credential.Email, roles, permissions, credential.LastLoginAtUtc));
    }
}
