using Domain.Entities.Example;
using Infrastructure.PostgreSql;

namespace Infrastructure.Persistence.SQLDB.Main.Example;

/// <summary>
/// Agrupa todos los queries SQL de la tabla <c>dbo.ExampleUsers</c>.
/// Ningún otro componente debe ejecutar SQL sobre esta tabla.
/// </summary>
public sealed class ExampleUsersSql
{
    private readonly MainDapperDbConnection _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ExampleUsersSql"/>.
    /// </summary>
    /// <param name="db">Conexión Dapper de la base de datos principal.</param>
    public ExampleUsersSql(MainDapperDbConnection db) => _db = db;

    /// <summary>
    /// Retorna el usuario con el <paramref name="publicId"/> dado, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="publicId">Identificador público del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario encontrado, o <c>null</c>.</returns>
    public Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<ExampleUser>(
            """
            SELECT Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.ExampleUsers
            WHERE PublicId = @publicId;
            """,
            new { publicId },
            cancellationToken: ct);

    /// <summary>
    /// Retorna <c>true</c> si ya existe un usuario con el correo <paramref name="email"/> (sin distinción de mayúsculas).
    /// </summary>
    /// <param name="email">Correo electrónico a verificar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><c>true</c> si el correo ya está registrado; de lo contrario <c>false</c>.</returns>
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM dbo.ExampleUsers
                WHERE LOWER(Email) = LOWER(@email)
            );
            """,
            new { email },
            cancellationToken: ct);

    /// <summary>
    /// Retorna una página de usuarios ordenados por <c>CreatedAtUtc</c> descendente.
    /// </summary>
    /// <param name="page">Número de página (base 1).</param>
    /// <param name="pageSize">Registros por página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Secuencia de usuarios de la página solicitada.</returns>
    public Task<IEnumerable<ExampleUser>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default) =>
        _db.QueryAsync<ExampleUser>(
            """
            SELECT Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.ExampleUsers
            ORDER BY CreatedAtUtc DESC
            LIMIT @pageSize OFFSET @offset;
            """,
            new { offset = (page - 1) * pageSize, pageSize },
            cancellationToken: ct);

    /// <summary>
    /// Retorna el total de usuarios en la tabla.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Número total de usuarios.</returns>
    public Task<int> GetCountAsync(CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM dbo.ExampleUsers;
            """,
            cancellationToken: ct);

    /// <summary>
    /// Inserta un nuevo usuario y retorna la entidad persistida vía <c>RETURNING</c>.
    /// </summary>
    /// <param name="fullName">Nombre completo.</param>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="department">Departamento.</param>
    /// <param name="notes">Notas opcionales.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario recién creado.</returns>
    public async Task<ExampleUser> InsertAsync(
        string fullName, string email, string department, string notes,
        CancellationToken ct = default) =>
        (await _db.QueryFirstAsync<ExampleUser>(
            """
            INSERT INTO dbo.ExampleUsers (FullName, Email, Department, Notes)
            VALUES (@fullName, @email, @department, @notes)
            RETURNING Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc;
            """,
            new { fullName, email, department, notes },
            cancellationToken: ct).ConfigureAwait(false))!;

    /// <summary>
    /// Actualiza los campos editables de un usuario activo. Retorna las filas afectadas (0 si no existe).
    /// </summary>
    /// <param name="publicId">Identificador del usuario a actualizar.</param>
    /// <param name="fullName">Nuevo nombre completo.</param>
    /// <param name="department">Nuevo departamento.</param>
    /// <param name="notes">Nuevas notas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Número de filas actualizadas (1 si exitoso, 0 si no encontrado).</returns>
    public Task<int> UpdateAsync(
        Guid publicId, string fullName, string department, string notes,
        CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.ExampleUsers
            SET FullName     = @fullName,
                Department   = @department,
                Notes        = @notes,
                UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId
              AND IsActive  = TRUE;
            """,
            new { publicId, fullName, department, notes },
            cancellationToken: ct);

    /// <summary>
    /// Desactiva un usuario activo (soft-delete). Retorna las filas afectadas (0 si no existe o ya estaba inactivo).
    /// </summary>
    /// <param name="publicId">Identificador del usuario a desactivar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Número de filas actualizadas (1 si exitoso, 0 si no encontrado).</returns>
    public Task<int> DisableAsync(Guid publicId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.ExampleUsers
            SET IsActive     = FALSE,
                UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId
              AND IsActive  = TRUE;
            """,
            new { publicId },
            cancellationToken: ct);
}
