CREATE UNIQUE INDEX IF NOT EXISTS UX_Credentials_PublicId          ON dbo.Credentials (PublicId);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Credentials_Email             ON dbo.Credentials (Email);
CREATE INDEX        IF NOT EXISTS IX_Credentials_TenantId          ON dbo.Credentials (TenantId);
CREATE INDEX        IF NOT EXISTS IX_Credentials_TenantId_IsActive ON dbo.Credentials (TenantId, IsActive);
