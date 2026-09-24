using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy.Domain.Entities;

namespace Tenancy.Infrastructure.Persistence;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenant");
        b.HasKey(e => e.Id);

        b.Property(e => e.PublicId).IsRequired();
        b.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UX_Tenant_PublicId");

        b.Property(e => e.Name).HasMaxLength(200).IsRequired();

        // Unico INCLUYENDO los borrados: un slug liberado por un soft delete no se reasigna a otro cliente
        // (los enlaces viejos apuntarian a una empresa distinta).
        b.Property(e => e.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("UX_Tenant_Slug");

        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.Ignore(e => e.IsActive);
    }
}
