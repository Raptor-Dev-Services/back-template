using Common.Results;

namespace Application.UseCases.Example.DisableExampleUser;

/// <summary>
/// Respuesta exitosa cuando el usuario fue desactivado correctamente.
/// </summary>
public sealed record DisableExampleUserSuccess : DisableExampleUserResponse, ISuccess;
