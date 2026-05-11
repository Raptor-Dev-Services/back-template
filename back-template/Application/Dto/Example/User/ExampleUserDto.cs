namespace Application.Dto.Example.User;

/// <summary>
/// Proyección de solo lectura de un usuario Example que devuelven los endpoints de la API.
/// </summary>
/// <param name="UserId">Identificador público del usuario.</param>
/// <param name="FullName">Nombre completo.</param>
/// <param name="Email">Correo electrónico.</param>
/// <param name="Department">Departamento.</param>
/// <param name="Notes">Notas opcionales en texto libre.</param>
/// <param name="IsActive">Indica si la cuenta está activa.</param>
/// <param name="CreatedAtUtc">Fecha de creación en UTC.</param>
/// <param name="UpdatedAtUtc">Fecha de última actualización en UTC.</param>
public sealed record ExampleUserDto(
    Guid     UserId,
    string   FullName,
    string   Email,
    string   Department,
    string   Notes,
    bool     IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
