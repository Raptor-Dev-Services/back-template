using Domain.Entities.Example;

namespace Domain.Repositories.Example;

/// <summary>
/// Contrato de acceso a datos para la entidad <see cref="ExampleUser"/>.
/// </summary>
public interface IExampleUserRepository
{
    /// <summary>
    /// Retorna el usuario identificado por <paramref name="publicId"/>, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="publicId">Identificador público expuesto en la API.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El usuario encontrado, o <c>null</c>.</returns>
    Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna <c>true</c> si ya existe un usuario con el correo <paramref name="email"/>.
    /// </summary>
    /// <param name="email">Correo electrónico a verificar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> si el correo ya está registrado; de lo contrario <c>false</c>.</returns>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna una página de usuarios ordenados por fecha de creación descendente.
    /// </summary>
    /// <param name="page">Número de página (base 1).</param>
    /// <param name="pageSize">Cantidad de registros por página.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Colección de solo lectura con los usuarios de la página solicitada.</returns>
    Task<IReadOnlyCollection<ExampleUser>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna el total de usuarios en la base de datos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Cantidad total de usuarios.</returns>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuevo usuario y retorna la entidad persistida.
    /// </summary>
    /// <param name="fullName">Nombre completo.</param>
    /// <param name="email">Correo electrónico único.</param>
    /// <param name="department">Nombre del departamento.</param>
    /// <param name="notes">Notas opcionales.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El usuario recién creado.</returns>
    Task<ExampleUser> InsertAsync(string fullName, string email, string department, string notes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los campos editables de un usuario activo.
    /// </summary>
    /// <param name="publicId">Identificador del usuario a actualizar.</param>
    /// <param name="fullName">Nuevo nombre completo.</param>
    /// <param name="department">Nuevo departamento.</param>
    /// <param name="notes">Nuevas notas.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> si se encontró y actualizó el registro; <c>false</c> si no existe.</returns>
    Task<bool> UpdateAsync(Guid publicId, string fullName, string department, string notes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desactiva (soft-delete) un usuario poniendo <c>IsActive</c> en <c>false</c>.
    /// </summary>
    /// <param name="publicId">Identificador del usuario a desactivar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> si se encontró y desactivó; <c>false</c> si no existe.</returns>
    Task<bool> DisableAsync(Guid publicId, CancellationToken cancellationToken = default);
}
