using Common.Messaging;
using Users.Application.UseCases.UpdateUserProfile.Responses;

namespace Users.Application.UseCases.UpdateUserProfile;

public sealed record UpdateUserProfileRequest(Guid PublicId, string FullName) : IRequest<UpdateUserProfileResponse>;
