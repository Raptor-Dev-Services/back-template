using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.InsertExampleUser;

public sealed record InsertExampleUserSuccess(ExampleUserDto Data) : InsertExampleUserResponse, ISuccess<ExampleUserDto>;
