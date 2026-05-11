using Common.Results;

namespace Application.UseCases.Example.GetExampleUser;

/// <summary>
/// Respuesta de error 404 cuando el usuario no existe.
/// </summary>
/// <param name="Message">Mensaje descriptivo del error.</param>
public sealed record GetExampleUserNotFoundFailure(string Message) : GetExampleUserResponse, INotFoundFailure;
