CREATE UNIQUE INDEX IF NOT EXISTS UX_Branches_PublicId          ON dbo.Branches (PublicId);
CREATE INDEX        IF NOT EXISTS IX_Branches_TenantId          ON dbo.Branches (TenantId);
CREATE INDEX        IF NOT EXISTS IX_Branches_TenantId_IsActive ON dbo.Branches (TenantId, IsActive);
