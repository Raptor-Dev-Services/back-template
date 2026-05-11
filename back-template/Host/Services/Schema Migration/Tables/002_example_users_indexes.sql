CREATE UNIQUE INDEX IF NOT EXISTS UX_ExampleUsers_PublicId ON dbo.ExampleUsers (PublicId);
CREATE INDEX IF NOT EXISTS IX_ExampleUsers_Email    ON dbo.ExampleUsers (LOWER(Email));
CREATE INDEX IF NOT EXISTS IX_ExampleUsers_IsActive ON dbo.ExampleUsers (IsActive);
CREATE INDEX IF NOT EXISTS IX_ExampleUsers_CreatedAt ON dbo.ExampleUsers (CreatedAtUtc DESC);
