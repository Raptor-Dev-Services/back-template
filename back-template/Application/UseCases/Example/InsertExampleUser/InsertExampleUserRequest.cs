using Common.Messaging;

namespace Application.UseCases.Example.InsertExampleUser;

/// <summary>
/// Solicita la creación de un nuevo usuario Example.
/// </summary>
/// <param name="FullName">Nombre completo del usuario.</param>
/// <param name="Email">Correo electrónico único.</param>
/// <param name="Department">Departamento al que pertenece.</param>
/// <param name="Notes">Notas opcionales.</param>
public sealed record InsertExampleUserRequest(
    string FullName,
    string Email,
    string Department,
    string Notes) : IRequest<InsertExampleUserResponse>;
