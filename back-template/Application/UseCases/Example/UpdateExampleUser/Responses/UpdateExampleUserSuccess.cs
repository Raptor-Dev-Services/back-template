using Common.Results;

namespace Application.UseCases.Example.UpdateExampleUser;

/// <summary>
/// Respuesta exitosa cuando el usuario fue actualizado correctamente.
/// </summary>
public sealed record UpdateExampleUserSuccess : UpdateExampleUserResponse, ISuccess;
