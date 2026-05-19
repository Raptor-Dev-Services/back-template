using Common.Messaging;
using Users.Application.UseCases.GetUserProfile.Responses;

namespace Users.Application.UseCases.GetUserProfile;

public sealed record GetUserProfileRequest(Guid PublicId, long TenantId) : IRequest<GetUserProfileResponse>;
