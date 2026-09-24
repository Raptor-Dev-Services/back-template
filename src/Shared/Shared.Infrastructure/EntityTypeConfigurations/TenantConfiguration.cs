using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy.Domain.Entities;

namespace Shared.Infrastructure.EntityTypeConfigurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.PublicId).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.Slug).IsUnique();
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("timestamp(0)").HasDefaultValueSql("timezone('utc', now())");
        b.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp(0)").HasDefaultValueSql("timezone('utc', now())");
    }
}
