using Common.Messaging;

namespace Application.UseCases.Example.DisableExampleUser;

public sealed record DisableExampleUserRequest(Guid PublicId) : IRequest<DisableExampleUserResponse>;
