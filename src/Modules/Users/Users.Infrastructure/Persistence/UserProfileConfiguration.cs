using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain.Entities;

namespace Users.Infrastructure.Persistence;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("UserProfile");
        b.HasKey(e => e.Id);

        b.Property(e => e.PublicId).IsRequired();
        b.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UX_UserProfile_PublicId");

        b.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        b.Property(e => e.IsActive).HasDefaultValue(true);

        // El listado pagina por tenant y fecha de alta: el indice cubre el filtro y el orden.
        b.HasIndex(e => new { e.TenantId, e.CreatedAtUtc }).HasDatabaseName("IX_UserProfile_TenantId_CreatedAtUtc");
    }
}
