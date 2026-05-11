namespace WebApi.EndPoints.Example.RequestBodies;

/// <summary>
/// Cuerpo JSON del request <c>POST /api/example/users</c>.
/// </summary>
/// <param name="FullName">Nombre completo del nuevo usuario.</param>
/// <param name="Email">Correo electrónico único.</param>
/// <param name="Department">Departamento al que pertenece.</param>
/// <param name="Notes">Notas opcionales en texto libre.</param>
public sealed record InsertExampleUserBody(string FullName, string Email, string Department, string Notes);
