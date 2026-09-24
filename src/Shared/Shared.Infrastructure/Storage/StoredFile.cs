using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Domain;
using Shared.Kernel.Storage;

namespace Shared.Infrastructure.Storage;

/// <summary>
/// Quien es dueno de cada objeto subido. Del tenant (<see cref="TenantEntity"/>): recibe el filtro de EF y la
/// policy de RLS, asi que una consulta de otro tenant no la ve ni con SQL crudo.
/// </summary>
public sealed class StoredFile : TenantEntity
{
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Nombre original, SOLO para mostrarlo. Nunca participa en la clave ni en la ruta.</summary>
    public string? FileName { get; set; }
}

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.ToTable("StoredFile");
        b.HasKey(e => e.Id);
        b.Property(e => e.ObjectKey).HasMaxLength(ObjectKeys.MaxLength).IsRequired();
        b.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        b.Property(e => e.FileName).HasMaxLength(255);
        // Unica en todo el sistema: una clave, un dueno.
        b.HasIndex(e => e.ObjectKey).IsUnique().HasDatabaseName("UX_StoredFile_ObjectKey");
        b.HasIndex(e => new { e.TenantId, e.CreatedAtUtc }).HasDatabaseName("IX_StoredFile_TenantId_CreatedAtUtc");
    }
}

/// <summary>El filtro de tenant y RLS acotan cada consulta: aqui no se repite el <c>TenantId</c>.</summary>
public sealed class EfStoredFileRegistry(AppDbContext db) : IStoredFileRegistry
{
    public void Add(string objectKey, string contentType, long sizeBytes, string? fileName) =>
        db.Add(new StoredFile { ObjectKey = objectKey, ContentType = contentType, SizeBytes = sizeBytes, FileName = fileName });

    public Task<StoredFileDto?> FindAsync(string objectKey, CancellationToken cancellationToken = default) =>
        db.Set<StoredFile>().AsNoTracking()
            .Where(f => f.ObjectKey == objectKey)
            .Select(f => new StoredFileDto(f.ObjectKey, f.ContentType, f.SizeBytes, f.FileName, f.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<string>> FilterOwnedAsync(IReadOnlyCollection<string> objectKeys, CancellationToken cancellationToken = default)
    {
        if (objectKeys.Count == 0)
            return [];

        return await db.Set<StoredFile>().AsNoTracking()
            .Where(f => objectKeys.Contains(f.ObjectKey))
            .Select(f => f.ObjectKey)
            .ToListAsync(cancellationToken);
    }
}
