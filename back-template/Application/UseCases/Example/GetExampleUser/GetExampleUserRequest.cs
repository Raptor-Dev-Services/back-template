using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUser;

/// <summary>
/// Solicita un usuario Example por su identificador público.
/// </summary>
/// <param name="PublicId">Identificador público del usuario a consultar.</param>
public sealed record GetExampleUserRequest(Guid PublicId) : IRequest<GetExampleUserResponse>;
