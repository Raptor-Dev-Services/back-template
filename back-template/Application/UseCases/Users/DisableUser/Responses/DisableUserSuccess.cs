using Common.Results;

namespace Application.UseCases.Users.DisableUser.Responses;

public sealed record DisableUserSuccess() : DisableUserResponse, ISuccess;
