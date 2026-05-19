using Common.Messaging;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Contracts.Dtos;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.GetUserProfiles;

public sealed class GetUserProfilesHandler : IRequestHandler<GetUserProfilesRequest, GetUserProfilesResponse>
{
    private readonly IUserProfileRepository _profiles;

    public GetUserProfilesHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<GetUserProfilesResponse> Handle(GetUserProfilesRequest request, CancellationToken cancellationToken)
    {
        var page     = Math.Clamp(request.Page, 1, int.MaxValue);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var listTask  = _profiles.GetPagedAsync(request.TenantId, page, pageSize, cancellationToken);
        var totalTask = _profiles.GetCountAsync(request.TenantId, cancellationToken);
        await Task.WhenAll(listTask, totalTask);

        var dtos = listTask.Result.Select(p => new UserProfileDto(
            p.PublicId, p.FullName, p.IsActive,
            p.CreatedAtUtc, p.UpdatedAtUtc)).ToArray();

        return new GetUserProfilesSuccess(dtos, totalTask.Result, page, pageSize);
    }
}
