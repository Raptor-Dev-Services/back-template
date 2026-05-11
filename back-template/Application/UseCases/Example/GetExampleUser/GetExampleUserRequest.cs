using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUser;

public sealed record GetExampleUserRequest(Guid PublicId) : IRequest<GetExampleUserResponse>;
