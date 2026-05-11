using Common.Results;

namespace Application.UseCases.Example.GetExampleUsers;

/// <summary>
/// Respuesta de error genérico al consultar la lista de usuarios.
/// </summary>
/// <param name="Message">Mensaje descriptivo del error.</param>
public sealed record GetExampleUsersFailure(string Message) : GetExampleUsersResponse, IFailure;
