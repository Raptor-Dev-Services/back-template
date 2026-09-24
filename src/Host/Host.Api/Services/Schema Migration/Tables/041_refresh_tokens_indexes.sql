CREATE UNIQUE INDEX IF NOT EXISTS UX_RefreshTokens_Token        ON dbo.RefreshTokens (Token);
CREATE INDEX        IF NOT EXISTS IX_RefreshTokens_CredentialId ON dbo.RefreshTokens (CredentialId);
CREATE INDEX        IF NOT EXISTS IX_RefreshTokens_IsRevoked    ON dbo.RefreshTokens (IsRevoked);
