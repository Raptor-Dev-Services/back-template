using Common.Results;

namespace Users.Application.UseCases.DisableUserProfile.Responses;

public sealed record DisableUserProfileSuccess() : DisableUserProfileResponse, ISuccess;
