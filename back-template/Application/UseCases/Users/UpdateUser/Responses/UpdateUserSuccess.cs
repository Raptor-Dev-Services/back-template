using Common.Results;

namespace Application.UseCases.Users.UpdateUser.Responses;

public sealed record UpdateUserSuccess() : UpdateUserResponse, ISuccess;
