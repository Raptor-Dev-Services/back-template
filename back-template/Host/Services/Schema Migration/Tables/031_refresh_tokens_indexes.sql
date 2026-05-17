CREATE UNIQUE INDEX IF NOT EXISTS UX_RefreshTokens_Token            ON dbo.RefreshTokens (Token);
CREATE INDEX        IF NOT EXISTS IX_RefreshTokens_UserId           ON dbo.RefreshTokens (UserId);
CREATE INDEX        IF NOT EXISTS IX_RefreshTokens_ExpiresAt_Revoked ON dbo.RefreshTokens (ExpiresAtUtc, IsRevoked);
