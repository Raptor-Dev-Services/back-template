using Common.Messaging;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Contracts.Dtos;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.GetUserProfiles;

internal sealed class GetUserProfilesHandler(IUserProfileRepository profiles)
    : IRequestHandler<GetUserProfilesRequest, GetUserProfilesResponse>
{
    public const int MaxPageSize = 100;

    public async Task<GetUserProfilesResponse> Handle(GetUserProfilesRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        // Secuencial a proposito: un DbContext no admite dos consultas a la vez (Task.WhenAll sobre el mismo
        // contexto lanza "A second operation was started on this context").
        var items = await profiles.GetPagedAsync(page, pageSize, cancellationToken);
        var total = await profiles.CountAsync(cancellationToken);

        var dtos = items
            .Select(p => new UserProfileDto(p.PublicId, p.FullName, p.IsActive, p.CreatedAtUtc, p.UpdatedAtUtc))
            .ToArray();

        return new GetUserProfilesSuccess(dtos, total, page, pageSize);
    }
}
