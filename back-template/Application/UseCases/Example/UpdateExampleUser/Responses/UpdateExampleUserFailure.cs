using Common.Results;

namespace Application.UseCases.Example.UpdateExampleUser;

/// <summary>
/// Respuesta de error 404 cuando el usuario a actualizar no existe o está inactivo.
/// </summary>
/// <param name="Message">Mensaje descriptivo del error.</param>
public sealed record UpdateExampleUserNotFoundFailure(string Message) : UpdateExampleUserResponse, INotFoundFailure;
