CREATE UNIQUE INDEX IF NOT EXISTS UX_UserProfiles_PublicId             ON dbo.UserProfiles (PublicId);
CREATE INDEX        IF NOT EXISTS IX_UserProfiles_TenantId             ON dbo.UserProfiles (TenantId);
CREATE INDEX        IF NOT EXISTS IX_UserProfiles_TenantId_IsActive    ON dbo.UserProfiles (TenantId, IsActive);
