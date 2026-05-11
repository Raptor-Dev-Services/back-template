using Common.Results;

namespace Application.UseCases.Example.DisableExampleUser;

/// <summary>
/// Respuesta de error 404 cuando el usuario a desactivar no existe o ya está inactivo.
/// </summary>
/// <param name="Message">Mensaje descriptivo del error.</param>
public sealed record DisableExampleUserNotFoundFailure(string Message) : DisableExampleUserResponse, INotFoundFailure;
