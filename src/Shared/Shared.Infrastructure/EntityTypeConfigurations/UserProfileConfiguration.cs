using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain.Entities;

namespace Shared.Infrastructure.EntityTypeConfigurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("user_profiles");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.PublicId).HasColumnType("uuid").IsRequired();
        b.HasIndex(e => e.PublicId).IsUnique();
        b.Property(e => e.TenantId).IsRequired();
        b.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAtUtc).HasColumnType("timestamp(0)").HasDefaultValueSql("timezone('utc', now())");
        b.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp(0)").HasDefaultValueSql("timezone('utc', now())");

        b.HasOne<Tenancy.Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
