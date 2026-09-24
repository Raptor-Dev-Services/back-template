using Authentication.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Infrastructure.Persistence;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permission");
        b.HasKey(e => e.Id);
        b.Property(e => e.Code).HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_Permission_Code");
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Role");
        b.HasKey(e => e.Id);
        b.Property(e => e.Code).HasMaxLength(50).IsRequired();
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        // Filtrado por IsDeleted: con soft delete, un indice unico "completo" impediria volver a crear lo que se borro.
        b.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Role_TenantId_Code");
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermission");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.RoleId, e.PermissionId }).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_RolePermission_TenantId_RoleId_PermissionId");
        b.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_RolePermission_Role_RoleId");
        b.HasOne<Permission>().WithMany().HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_RolePermission_Permission_PermissionId");
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRole");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.CredentialId, e.RoleId }).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_UserRole_TenantId_CredentialId_RoleId");
        b.HasOne<UserCredential>().WithMany().HasForeignKey(e => e.CredentialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_UserRole_UserCredential_CredentialId");
        b.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_UserRole_Role_RoleId");
    }
}
