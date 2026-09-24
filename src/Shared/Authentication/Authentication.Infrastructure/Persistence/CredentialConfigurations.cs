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

        // Unico en TODO el sistema e incluyendo las borradas: el login identifica al usuario solo por su correo.
        b.Property(e => e.Email).HasMaxLength(254).IsRequired();
        b.HasIndex(e => e.Email).IsUnique().HasDatabaseName("UX_UserCredential_Email");

        b.Property(e => e.PasswordHash).HasMaxLength(100).IsRequired();
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.TotpSecretProtected).HasMaxLength(200);

        b.Ignore(e => e.CanSignIn);
        b.Ignore(e => e.IsTwoFactorEnabled);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshToken");
        b.HasKey(e => e.Id);

        // SHA-256 en hex: 64 caracteres. El token en claro nunca llega a la base.
        b.Property(e => e.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        b.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("UX_RefreshToken_TokenHash");
        b.Property(e => e.ReplacedByTokenHash).HasMaxLength(64).IsFixedLength();
        b.Property(e => e.RevokedReason).HasMaxLength(32);
        b.Property(e => e.CreatedByIp).HasMaxLength(64);

        // Para revocar de golpe las sesiones vivas de una credencial (reuso, cambio de contrasena, bloqueo).
        b.HasIndex(e => new { e.CredentialId, e.RevokedAtUtc }).HasDatabaseName("IX_RefreshToken_CredentialId_RevokedAtUtc");

        // Restrict y no Cascade: las credenciales no se borran fisicamente, y un DELETE en cascada borraria el
        // historial de sesiones que sirve para investigar un acceso.
        b.HasOne<UserCredential>().WithMany().HasForeignKey(e => e.CredentialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_RefreshToken_UserCredential_CredentialId");
    }
}

public sealed class PasswordSetupTokenConfiguration : IEntityTypeConfiguration<PasswordSetupToken>
{
    public void Configure(EntityTypeBuilder<PasswordSetupToken> b)
    {
        b.ToTable("PasswordSetupToken");
        b.HasKey(e => e.Id);

        b.Property(e => e.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        b.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("UX_PasswordSetupToken_TokenHash");
        b.Property(e => e.Purpose).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.HasIndex(e => new { e.CredentialId, e.ConsumedAtUtc }).HasDatabaseName("IX_PasswordSetupToken_CredentialId_ConsumedAtUtc");

        b.HasOne<UserCredential>().WithMany().HasForeignKey(e => e.CredentialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PasswordSetupToken_UserCredential_CredentialId");
    }
}

public sealed class TwoFactorRecoveryCodeConfiguration : IEntityTypeConfiguration<TwoFactorRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TwoFactorRecoveryCode> b)
    {
        b.ToTable("TwoFactorRecoveryCode");
        b.HasKey(e => e.Id);
        b.Property(e => e.CodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
        b.HasIndex(e => new { e.CredentialId, e.ConsumedAtUtc }).HasDatabaseName("IX_TwoFactorRecoveryCode_CredentialId_ConsumedAtUtc");
        b.HasOne<UserCredential>().WithMany().HasForeignKey(e => e.CredentialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_TwoFactorRecoveryCode_UserCredential_CredentialId");
    }
}
