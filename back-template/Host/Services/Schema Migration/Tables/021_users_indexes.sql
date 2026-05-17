CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_PublicId         ON dbo.Users (PublicId);
CREATE INDEX        IF NOT EXISTS IX_Users_Email             ON dbo.Users (LOWER(Email));
CREATE INDEX        IF NOT EXISTS IX_Users_TenantId          ON dbo.Users (TenantId);
CREATE INDEX        IF NOT EXISTS IX_Users_TenantId_IsActive ON dbo.Users (TenantId, IsActive);
CREATE INDEX        IF NOT EXISTS IX_Users_BranchId          ON dbo.Users (BranchId);
