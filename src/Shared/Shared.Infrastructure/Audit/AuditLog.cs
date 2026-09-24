using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Audit;
using Shared.Kernel.Context;
using Shared.Kernel.Domain;
using Shared.Kernel.Results;

namespace Shared.Infrastructure.Audit;

/// <summary>
/// Una linea de la bitacora de acciones. Tenant-aware (filtro de EF y policy de RLS): cada tenant ve solo la suya.
/// Inmutable por convencion: nadie la edita, y el rol de la aplicacion no puede borrarla (no tiene DELETE).
/// </summary>
public sealed class AuditLog : TenantEntity
{
    public DateTime OccurredAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>Quien la ejecuto (PublicId). Null solo en acciones de sistema (tareas programadas).</summary>
    public Guid? ActorUserId { get; set; }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLog");
        b.HasKey(e => e.Id);
        b.Property(e => e.Action).HasMaxLength(100).IsRequired();
        b.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        b.Property(e => e.EntityId).HasMaxLength(100);
        b.Property(e => e.Summary).HasMaxLength(1000).IsRequired();
        b.Property(e => e.Reason).HasMaxLength(500);
        // La pantalla de consulta pagina por tenant y fecha, y filtra por accion.
        b.HasIndex(e => new { e.TenantId, e.OccurredAtUtc }).HasDatabaseName("IX_AuditLog_TenantId_OccurredAtUtc");
        b.HasIndex(e => new { e.TenantId, e.Action }).HasDatabaseName("IX_AuditLog_TenantId_Action");
    }
}

/// <summary>Implementacion EF: agrega sin guardar (ver <see cref="IAuditLog"/>).</summary>
public sealed class EfAuditLog(AppDbContext db, ICurrentUser currentUser) : IAuditLog, IAuditLogReader
{
    public void Append(string action, string entityType, string? entityId, string summary, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("La accion y el tipo de entidad son obligatorios en la bitacora.");

        db.Set<AuditLog>().Add(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            Summary = Truncate(summary, 1000),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : Truncate(reason, 500),
            ActorUserId = currentUser.UserId,
        });
    }

    public async Task<PagedResult<AuditEntryDto>> GetPageAsync(int page, int pageSize, string? action, CancellationToken cancellationToken = default)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var query = db.Set<AuditLog>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action == action);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Skip((p - 1) * size).Take(size)
            .Select(e => new AuditEntryDto(e.Id, e.OccurredAtUtc, e.Action, e.EntityType, e.EntityId, e.Summary, e.Reason, e.ActorUserId))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEntryDto>(items, p, size, total);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
