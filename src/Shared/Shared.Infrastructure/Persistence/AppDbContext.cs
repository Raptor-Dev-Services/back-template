using System.Reflection;
using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Kernel.Context;
using Shared.Kernel.Domain;
using Shared.Kernel.Errors;

namespace Shared.Infrastructure.Persistence;

/// <summary>Nombres de los filtros globales de EF, para ignorar SOLO el que hace falta.</summary>
public static class QueryFilterNames
{
    /// <summary>Aislamiento por tenant. Solo lo ignora la autenticacion (antes de que exista tenant).</summary>
    public const string Tenant = "tenant";

    /// <summary>Oculta lo borrado (soft delete).</summary>
    public const string SoftDelete = "soft_delete";
}

/// <summary>
/// El UNICO DbContext del sistema. Aplica por CONVENCION, sin que ningun modulo tenga que declararlo:
///
/// <list type="bullet">
///   <item><b>Aislamiento por tenant</b>: toda entidad que herede de <see cref="TenantEntity"/> recibe el filtro
///   <see cref="QueryFilterNames.Tenant"/> (<c>TenantId == tenant de la peticion</c>). Es la primera barrera;
///   la segunda es RLS en Postgres (<c>Persistence/Sql/001_enable_rls.sql</c>) y el interceptor que fija
///   <c>app.tenant_id</c> en cada conexion.</item>
///   <item><b>Soft delete</b>: todo <see cref="ISoftDeletable"/> recibe el filtro <see cref="QueryFilterNames.SoftDelete"/>
///   y un <c>Remove</c> se convierte en una marca, nunca en un DELETE.</item>
///   <item><b>Auditoria</b>: <c>CreatedAtUtc/UpdatedAtUtc/DeletedAtUtc</c> en UTC y el actor
///   (<c>CreatedByUserId</c>...) desde el usuario autenticado.</item>
///   <item><b>UTC</b>: toda fecha es <c>timestamp with time zone</c>. Npgsql exige <c>Kind=Utc</c> al escribirla;
///   las fechas que entran por la API ya llegan en UTC por los binders de <c>Shared.Web.UtcDateTime</c>.</item>
///   <item><b>Concurrencia optimista</b>: <c>Version</c> se mapea a <c>xmin</c>; perder la carrera es 409.</item>
///   <item><b>Unicidad</b>: una violacion de indice unico (23505) es 409 con mensaje legible, no un 500.</item>
/// </list>
///
/// Las configuraciones de cada tabla las aporta su modulo (<see cref="ModuleModel"/>), asi este proyecto
/// compartido no referencia a ninguno.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo ApplySoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ICurrentUser _currentUser;
    private readonly IReadOnlyList<Assembly> _modelAssemblies;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantContextAccessor tenantAccessor,
        ICurrentUser currentUser,
        IEnumerable<ModuleModel> modules)
        : base(options)
    {
        _tenantAccessor = tenantAccessor;
        _currentUser = currentUser;
        _modelAssemblies = modules.Select(m => m.Assembly).Distinct().OrderBy(a => a.FullName).ToArray();
        ModelKey = string.Join('|', _modelAssemblies.Select(a => a.GetName().Name));
    }

    /// <summary>Identifica el juego de modulos del modelo (ver <see cref="ModuleAwareModelCacheKeyFactory"/>).</summary>
    public string ModelKey { get; }

    /// <summary>
    /// Tenant de la peticion. Se lee en CADA consulta (EF parametriza el filtro con esta propiedad), asi que
    /// siempre refleja el contexto actual. Sin tenant vale 0, que no coincide con ninguna fila: fail-closed.
    /// </summary>
    private long CurrentTenantId =>
        long.TryParse(_tenantAccessor.Current?.TenantId, out var id) ? id : 0L;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Explicito aunque sea el default de Npgsql: la plantilla venia de timestamp(0) SIN zona mientras el
        // codigo escribia Kind=Utc, y Npgsql rechaza esa combinacion en tiempo de ejecucion.
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp with time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp with time zone");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        // Tablas del propio proyecto compartido (bitacora, archivos, tareas) y las de cada modulo.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        foreach (var assembly in _modelAssemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.IsOwned())
                continue; // un owned type se filtra con su dueno

            var clr = entityType.ClrType;

            if (typeof(TenantEntity).IsAssignableFrom(clr))
            {
                ApplyTenantFilterMethod.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
                modelBuilder.Entity(clr).HasIndex(nameof(TenantEntity.TenantId));
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clr))
                ApplySoftDeleteFilterMethod.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);

            if (typeof(TenantEntity).IsAssignableFrom(clr) || typeof(GlobalEntity).IsAssignableFrom(clr))
                modelBuilder.Entity(clr).Property(nameof(TenantEntity.Version)).IsRowVersion();
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : TenantEntity =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(QueryFilterNames.Tenant, e => e.TenantId == CurrentTenantId);

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ISoftDeletable =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(QueryFilterNames.SoftDelete, e => !e.IsDeleted);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenancyAndAudit();
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException ex) when (Translate(ex) is { } business)
        {
            throw business;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenancyAndAudit();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (Translate(ex) is { } business)
        {
            throw business;
        }
    }

    /// <summary>
    /// Traduce los fallos de escritura que son de NEGOCIO a un 409 con un mensaje que el operador entiende.
    /// El resto se deja subir: es un error nuestro y termina en 500 con el detalle en el log.
    /// </summary>
    private static ConflictException? Translate(DbUpdateException exception) => exception switch
    {
        DbUpdateConcurrencyException =>
            new ConflictException("La informacion cambio mientras se procesaba la operacion. Vuelve a consultarla antes de repetirla."),
        { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
            new ConflictException("Ya existe un registro con esos datos."),
        _ => null,
    };

    /// <summary>
    /// Sella tenant, auditoria y soft delete POR INTERFAZ, asi la misma logica cubre entidades tenant-aware y
    /// globales. Lo que no implementa una marca no se toca.
    /// </summary>
    private void ApplyTenancyAndAudit()
    {
        var nowUtc = DateTime.UtcNow;
        var tenantId = CurrentTenantId;
        var actor = _currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is ITenantScoped added)
                    {
                        // El tenant sale del contexto autenticado, nunca del cliente. Solo se respeta un valor
                        // puesto a proposito (el bootstrap y las tareas de sistema escriben sin peticion).
                        if (added.TenantId == 0)
                            added.TenantId = tenantId;
                        if (added.TenantId == 0)
                            throw new InvalidOperationException(
                                $"Se intento insertar {entry.Metadata.DisplayName()} sin tenant. Una fila tenant-aware sin " +
                                "TenantId queda huerfana y fuera de toda policy: fija el contexto de tenant antes de escribir.");
                    }

                    if (entry.Entity is IAuditable addedAuditable)
                    {
                        addedAuditable.CreatedAtUtc = nowUtc;
                        addedAuditable.UpdatedAtUtc = nowUtc;
                    }

                    if (entry.Entity is IAuditableUser addedBy)
                        addedBy.CreatedByUserId ??= actor;
                    break;

                case EntityState.Modified:
                    // Mover una fila de tenant no es una edicion: es una fuga. No hay caso de uso que lo necesite.
                    if (entry.Entity is ITenantScoped && entry.Property(nameof(ITenantScoped.TenantId)).IsModified)
                        throw new InvalidOperationException(
                            $"Se intento cambiar el TenantId de {entry.Metadata.DisplayName()}. Las filas no cambian de tenant.");

                    if (entry.Entity is IAuditable modifiedAuditable)
                        modifiedAuditable.UpdatedAtUtc = nowUtc;
                    if (entry.Entity is IAuditableUser modifiedBy)
                        modifiedBy.UpdatedByUserId = actor;

                    // Soft delete "a mano": el handler puso IsDeleted = true en vez de llamar a Remove.
                    if (entry.Entity is ISoftDeletable manual
                        && manual.IsDeleted
                        && entry.Property(nameof(ISoftDeletable.IsDeleted)).IsModified
                        && manual.DeletedAtUtc is null)
                    {
                        manual.DeletedAtUtc = nowUtc;
                        if (entry.Entity is IAuditableUser manualBy)
                            manualBy.DeletedByUserId = actor;
                    }
                    break;

                case EntityState.Deleted when entry.Entity is ISoftDeletable softDeletable:
                    // Nunca DELETE fisico en tablas de negocio.
                    entry.State = EntityState.Modified;
                    softDeletable.IsDeleted = true;
                    softDeletable.DeletedAtUtc = nowUtc;
                    if (entry.Entity is IAuditable deletedAuditable)
                        deletedAuditable.UpdatedAtUtc = nowUtc;
                    if (entry.Entity is IAuditableUser deletedBy)
                    {
                        deletedBy.UpdatedByUserId = actor;
                        deletedBy.DeletedByUserId = actor;
                    }
                    break;
            }
        }
    }
}
