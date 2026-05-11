using Common.Messaging;

namespace Application.UseCases.Example.DisableExampleUser;

/// <summary>
/// Solicita la desactivación (soft-delete) de un usuario Example.
/// </summary>
/// <param name="PublicId">Identificador público del usuario a desactivar.</param>
public sealed record DisableExampleUserRequest(Guid PublicId) : IRequest<DisableExampleUserResponse>;
