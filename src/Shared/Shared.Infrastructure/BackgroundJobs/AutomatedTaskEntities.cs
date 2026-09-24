using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Infrastructure.BackgroundJobs;

/// <summary>
/// Configuracion y estado de una tarea: una fila por tarea. El CATALOGO de que tareas existen vive en codigo (cada
/// <c>IAutomatedTask</c> registrado); esta tabla guarda si esta pausada, cada cuanto corre, su ultima corrida y el
/// reclamo entre replicas. GLOBAL (sin TenantId): es infraestructura de la plataforma, no dato de un cliente.
/// </summary>
public sealed class AutomatedTaskDefinition
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int IntervalMinutes { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime NextRunAtUtc { get; set; }

    /// <summary>Hasta donde llego la ultima corrida COMPLETA. Solo avanza con Success o Skipped.</summary>
    public DateTime? LastCutoffUtc { get; set; }

    /// <summary>No nulo mientras una instancia la ejecuta. Un reclamo mas viejo que el timeout se da por huerfano.</summary>
    public DateTime? ClaimedAtUtc { get; set; }
}

/// <summary>Bitacora de corridas: "y lo del martes, quien lo proceso?". Una fila por corrida.</summary>
public sealed class AutomatedTaskRun
{
    public long Id { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ItemsProcessed { get; set; }
    public int ItemsFailed { get; set; }
    public string? Message { get; set; }
    public DateTime WindowFromUtc { get; set; }
    public DateTime WindowToUtc { get; set; }

    /// <summary>Quien la disparo a mano; null si la corrio el despachador.</summary>
    public Guid? TriggeredByUserId { get; set; }
}

public sealed class AutomatedTaskDefinitionConfiguration : IEntityTypeConfiguration<AutomatedTaskDefinition>
{
    public void Configure(EntityTypeBuilder<AutomatedTaskDefinition> b)
    {
        b.ToTable("AutomatedTaskDefinition");
        b.HasKey(e => e.Id);
        b.Property(e => e.Code).HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_AutomatedTaskDefinition_Code");
        b.HasIndex(e => new { e.IsEnabled, e.NextRunAtUtc }).HasDatabaseName("IX_AutomatedTaskDefinition_IsEnabled_NextRunAtUtc");
    }
}

public sealed class AutomatedTaskRunConfiguration : IEntityTypeConfiguration<AutomatedTaskRun>
{
    public void Configure(EntityTypeBuilder<AutomatedTaskRun> b)
    {
        b.ToTable("AutomatedTaskRun");
        b.HasKey(e => e.Id);
        b.Property(e => e.TaskCode).HasMaxLength(100).IsRequired();
        b.Property(e => e.Status).HasMaxLength(20).IsRequired();
        b.Property(e => e.Message).HasMaxLength(2000);
        b.HasIndex(e => new { e.TaskCode, e.StartedAtUtc }).HasDatabaseName("IX_AutomatedTaskRun_TaskCode_StartedAtUtc");
    }
}
