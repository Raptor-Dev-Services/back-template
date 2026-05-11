namespace Domain.Entities.Example;

/// <summary>
/// Entidad de dominio que representa un usuario del módulo Example.
/// </summary>
public sealed class ExampleUser
{
    /// <summary>Clave sustituta interna. Nunca se expone en la API.</summary>
    public long     Id           { get; init; }

    /// <summary>Identificador público usado en respuestas y URLs de la API.</summary>
    public Guid     PublicId     { get; init; }

    /// <summary>Nombre completo del usuario.</summary>
    public string   FullName     { get; init; } = string.Empty;

    /// <summary>Correo electrónico único del usuario.</summary>
    public string   Email        { get; init; } = string.Empty;

    /// <summary>Departamento al que pertenece el usuario.</summary>
    public string   Department   { get; init; } = string.Empty;

    /// <summary>Notas opcionales en texto libre.</summary>
    public string   Notes        { get; init; } = string.Empty;

    /// <summary>Indica si la cuenta está activa.</summary>
    public bool     IsActive     { get; init; }

    /// <summary>Fecha y hora de creación en UTC.</summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>Fecha y hora de última actualización en UTC.</summary>
    public DateTime UpdatedAtUtc { get; init; }
}
