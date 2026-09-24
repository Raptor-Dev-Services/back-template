using Common.Messaging;
using Shared.Kernel.Results;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Contracts.Dtos;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.GetUserProfiles;

internal sealed class GetUserProfilesHandler(IUserProfileRepository profiles)
    : IRequestHandler<GetUserProfilesRequest, GetUserProfilesResponse>
{
    public async Task<GetUserProfilesResponse> Handle(GetUserProfilesRequest request, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);

        // Secuencial a proposito: un DbContext no admite dos consultas a la vez (Task.WhenAll sobre el mismo
        // contexto lanza "A second operation was started on this context").
        var items = await profiles.GetPagedAsync(page, pageSize, cancellationToken);
        var total = await profiles.CountAsync(cancellationToken);

        var dtos = items.Select(UserProfileMapping.ToDto).ToArray();
        return new GetUserProfilesSuccess(new PagedResult<UserProfileDto>(dtos, page, pageSize, total));
    }
}
