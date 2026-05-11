using Common.Messaging;

namespace Application.UseCases.Example.UpdateExampleUser;

public sealed record UpdateExampleUserRequest(
    Guid   PublicId,
    string FullName,
    string Department,
    string Notes) : IRequest<UpdateExampleUserResponse>;
