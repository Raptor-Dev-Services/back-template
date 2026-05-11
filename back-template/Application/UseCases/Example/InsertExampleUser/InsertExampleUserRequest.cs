using Common.Messaging;

namespace Application.UseCases.Example.InsertExampleUser;

public sealed record InsertExampleUserRequest(
    string FullName,
    string Email,
    string Department,
    string Notes) : IRequest<InsertExampleUserResponse>;
