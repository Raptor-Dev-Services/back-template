using Authentication.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Infrastructure.Persistence;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> b)
    {
        b.ToTable("UserCredential");
        b.HasKey(e => e.Id);

        b.Property(e => e.PublicId).IsRequired();
        b.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UX_UserCredential_PublicId");

        // Unico en todo el sistema: el login identifica al usuario solo por su correo (normalizado a minusculas).
        b.Property(e => e.Email).HasMaxLength(254).IsRequired();
        b.HasIndex(e => e.Email).IsUnique().HasDatabaseName("UX_UserCredential_Email");

        b.Property(e => e.PasswordHash).HasMaxLength(100).IsRequired();
        b.Property(e => e.Role).HasMaxLength(50).IsRequired();
        b.Property(e => e.IsActive).HasDefaultValue(true);
    }
}
