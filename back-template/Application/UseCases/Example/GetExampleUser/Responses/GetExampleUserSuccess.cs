using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.GetExampleUser;

public sealed record GetExampleUserSuccess(ExampleUserDto Data) : GetExampleUserResponse, ISuccess<ExampleUserDto>;
