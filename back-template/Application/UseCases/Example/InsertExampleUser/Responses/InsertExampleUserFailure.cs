using Common.Results;

namespace Application.UseCases.Example.InsertExampleUser;

/// <summary>
/// Respuesta de error 409 cuando el correo electrónico ya está registrado.
/// </summary>
/// <param name="Message">Mensaje descriptivo del conflicto.</param>
public sealed record InsertExampleUserConflictFailure(string Message) : InsertExampleUserResponse, IConflictFailure;
