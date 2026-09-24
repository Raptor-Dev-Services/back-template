CREATE UNIQUE INDEX IF NOT EXISTS UX_Tenants_PublicId ON dbo.Tenants (PublicId);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Tenants_Slug    ON dbo.Tenants (Slug);
CREATE INDEX        IF NOT EXISTS IX_Tenants_IsActive ON dbo.Tenants (IsActive);
