using Authentication.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Database.EntityTypeConfigurations;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> b)
    {
        b.ToTable("credentials");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.PublicId).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.TenantId).IsRequired();
        b.Property(e => e.Email).HasMaxLength(254).IsRequired();
        b.HasIndex(e => e.Email).IsUnique();
        b.Property(e => e.PasswordHash).HasMaxLength(100).IsRequired();
        b.Property(e => e.Role).HasMaxLength(50).IsRequired().HasDefaultValue("User");
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("timestamp(0)").HasDefaultValueSql("timezone('utc', now())");
        b.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp(0)").HasDefaultValueSql("timezone('utc', now())");

        b.HasOne<Tenancy.Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
