using Common.Messaging;
using Users.Application.UseCases.GetUserProfiles.Responses;

namespace Users.Application.UseCases.GetUserProfiles;

public sealed record GetUserProfilesRequest(int Page, int PageSize) : IRequest<GetUserProfilesResponse>;
