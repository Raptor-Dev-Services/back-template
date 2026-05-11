using Common.Messaging;

namespace Application.UseCases.Example.UpdateExampleUser;

/// <summary>
/// Solicita la actualización de los campos editables de un usuario Example.
/// </summary>
/// <param name="PublicId">Identificador público del usuario a actualizar.</param>
/// <param name="FullName">Nuevo nombre completo.</param>
/// <param name="Department">Nuevo departamento.</param>
/// <param name="Notes">Nuevas notas opcionales.</param>
public sealed record UpdateExampleUserRequest(
    Guid   PublicId,
    string FullName,
    string Department,
    string Notes) : IRequest<UpdateExampleUserResponse>;
