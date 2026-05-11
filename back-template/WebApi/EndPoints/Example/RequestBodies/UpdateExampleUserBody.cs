namespace WebApi.EndPoints.Example.RequestBodies;

/// <summary>
/// Cuerpo JSON del request <c>PUT /api/example/users/{id}</c>.
/// </summary>
/// <param name="FullName">Nuevo nombre completo.</param>
/// <param name="Department">Nuevo departamento.</param>
/// <param name="Notes">Nuevas notas opcionales.</param>
public sealed record UpdateExampleUserBody(string FullName, string Department, string Notes);
