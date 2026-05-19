using Common.Messaging;
using Users.Application.UseCases.DisableUserProfile.Responses;

namespace Users.Application.UseCases.DisableUserProfile;

public sealed record DisableUserProfileRequest(Guid PublicId, long TenantId) : IRequest<DisableUserProfileResponse>;
