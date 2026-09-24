namespace Shared.Kernel.Domain;

/// <summary>
/// Base de toda entidad de negocio: pertenece a un tenant y trae la auditoria estandar.
///
/// <para>Heredar de aqui es lo que la pone bajo las dos barreras de aislamiento, sin que el repositorio
/// tenga que acordarse de nada:</para>
/// <list type="number">
///   <item>el filtro global de EF (<c>TenantId == tenant actual &amp;&amp; !IsDeleted</c>), aplicado por
///   CONVENCION a todo tipo que herede de esta clase;</item>
///   <item>la policy de RLS de Postgres, que se declara para su tabla en
///   <c>Persistence/Sql/001_enable_rls.sql</c> (una prueba falla si una tabla con <c>TenantId</c> queda sin
///   ella).</item>
/// </list>
///
/// <para><see cref="TenantId"/>, las marcas de auditoria y el soft delete los rellena
/// <c>AppDbContext.SaveChanges</c> desde el contexto autenticado, NUNCA el cliente.</para>
///
/// <para><see cref="Version"/> es el token de concurrencia optimista: se mapea a la columna de sistema
/// <c>xmin</c> de Postgres (no ocupa columna propia). Dos operadores que editan la misma fila a la vez:
/// el segundo recibe 409 en vez de pisar al primero en silencio.</para>
/// </summary>
public abstract class TenantEntity : ITenantScoped, IAuditable, IAuditableUser, ISoftDeletable
{
    public long Id { get; set; }

    public long TenantId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public uint Version { get; set; }
}

/// <summary>
/// Base de una entidad GLOBAL: no pertenece a ningun tenant (el propio <c>Tenant</c>, catalogos de
/// plataforma). Trae auditoria temporal y soft delete, y el filtro global solo oculta lo borrado. Sin
/// columnas de "quien": a estas filas las escribe el sistema, no un usuario de un tenant.
/// </summary>
public abstract class GlobalEntity : IAuditable, ISoftDeletable
{
    public long Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public uint Version { get; set; }
}
