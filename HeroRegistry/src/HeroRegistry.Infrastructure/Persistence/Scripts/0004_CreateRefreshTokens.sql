-- 0004: Refresh tokens (T09.1, AC12). Only the SHA-256 hash of each token is stored; the raw value lives
-- in the user's HttpOnly cookie. Applied once by DatabaseMigrator. Never edit an applied script.

CREATE TABLE dbo.RefreshTokens
(
    Id              INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId          INT              NOT NULL CONSTRAINT FK_RefreshTokens_UserId REFERENCES dbo.Users (Id),
    -- Lower-case hex SHA-256 (64 characters) of the raw token.
    TokenHash       CHAR(64)         NOT NULL,
    -- One family per login; every rotation stays in it, so reuse can revoke the whole chain.
    FamilyId        UNIQUEIDENTIFIER NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL,
    ExpiresAt       DATETIME2(3)     NOT NULL,
    RevokedAt       DATETIME2(3)     NULL,
    -- Set when the token was rotated (not when revoked by logout or theft detection).
    ReplacedByHash  CHAR(64)         NULL
);
GO

-- Lookup by hash on every refresh. Unique: two tokens can never share a hash.
CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON dbo.RefreshTokens (TokenHash);
GO

-- Revoking or cleaning up a user's tokens.
CREATE INDEX IX_RefreshTokens_UserId_RevokedAt ON dbo.RefreshTokens (UserId, RevokedAt);
GO

-- Revoking a family (logout, reuse detection).
CREATE INDEX IX_RefreshTokens_FamilyId ON dbo.RefreshTokens (FamilyId);
GO
