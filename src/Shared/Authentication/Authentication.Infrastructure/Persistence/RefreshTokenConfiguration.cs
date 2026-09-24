using Authentication.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Infrastructure.Persistence;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshToken");
        b.HasKey(e => e.Id);

        b.Property(e => e.Token).HasMaxLength(256).IsRequired();
        b.HasIndex(e => e.Token).IsUnique().HasDatabaseName("UX_RefreshToken_Token");

        // Restrict y no Cascade: las credenciales no se borran fisicamente (soft delete), y un DELETE en
        // cascada borraria el historial de sesiones que sirve para investigar un acceso.
        b.HasOne<UserCredential>()
            .WithMany()
            .HasForeignKey(e => e.CredentialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RefreshToken_UserCredential_CredentialId");
    }
}
