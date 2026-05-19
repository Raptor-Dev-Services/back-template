using Authentication.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Database.EntityTypeConfigurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.CredentialId).IsRequired();
        b.Property(e => e.Token).HasMaxLength(256).IsRequired();
        b.HasIndex(e => e.Token).IsUnique();
        b.Property(e => e.ExpiresAtUtc).HasColumnType("timestamp(0)").IsRequired();
        b.Property(e => e.IsRevoked).HasDefaultValue(false);
        b.Property(e => e.CreatedAtUtc).HasColumnType("timestamp(0)").HasDefaultValueSql("timezone('utc', now())");

        b.HasOne<UserCredential>()
            .WithMany()
            .HasForeignKey(e => e.CredentialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
