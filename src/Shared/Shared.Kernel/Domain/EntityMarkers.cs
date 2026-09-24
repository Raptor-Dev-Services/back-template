namespace Shared.Kernel.Domain;

/// <summary>
/// Auditoria temporal en UTC. La mantiene <c>AppDbContext.SaveChanges</c>: sella
/// <see cref="CreatedAtUtc"/> = <see cref="UpdatedAtUtc"/> al insertar y refresca
/// <see cref="UpdatedAtUtc"/> en cada modificacion (incluido el soft delete). El codigo de aplicacion no
/// las asigna a mano.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// Registra QUE usuario creo, modifico y borro la fila (su PublicId). Lo sella
/// <c>AppDbContext.SaveChanges</c> desde el usuario autenticado de la peticion; en contexto de sistema
/// (tareas programadas, seeders) no hay actor y los campos quedan en null.
/// </summary>
public interface IAuditableUser
{
    Guid? CreatedByUserId { get; set; }
    Guid? UpdatedByUserId { get; set; }
    Guid? DeletedByUserId { get; set; }
}

/// <summary>
/// Soft delete: nunca DELETE fisico en tablas de negocio. <c>AppDbContext.SaveChanges</c> convierte un
/// <c>Remove</c> en una modificacion que marca <see cref="IsDeleted"/> y sella <see cref="DeletedAtUtc"/>;
/// el filtro global oculta las filas borradas a toda consulta.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}

/// <summary>Fila que pertenece a UN tenant. La frontera de aislamiento del sistema.</summary>
public interface ITenantScoped
{
    long TenantId { get; set; }
}
