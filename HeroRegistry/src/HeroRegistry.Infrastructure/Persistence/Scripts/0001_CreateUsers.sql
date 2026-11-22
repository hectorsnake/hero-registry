-- 0001: Users table (accounts that can log in to the Hero Registry).
-- Applied once by DatabaseMigrator. Never edit an applied script: add a new numbered script instead.

CREATE TABLE dbo.Users
(
    Id          INT IDENTITY(1, 1)  NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    -- Case-insensitive collation, so "Admin" and "admin" are the same user name.
    UserName    NVARCHAR(50)  COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    Email       NVARCHAR(256) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    -- BCrypt hash. A plain-text password is never stored.
    Password    NVARCHAR(200) NOT NULL,
    IsAdmin     BIT           NOT NULL CONSTRAINT DF_Users_IsAdmin   DEFAULT (0),
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
    -- NULL for system-created accounts (seed data) and self-registration.
    CreatedBy   INT           NULL     CONSTRAINT FK_Users_CreatedBy REFERENCES dbo.Users (Id),
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedBy   INT           NULL     CONSTRAINT FK_Users_UpdatedBy REFERENCES dbo.Users (Id),
    UpdatedAt   DATETIME2(3)  NULL
);
GO

-- Unique among active users only: a soft-deleted account frees its user name and email.
CREATE UNIQUE INDEX UX_Users_UserName ON dbo.Users (UserName) WHERE IsDeleted = 0;
GO

CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users (Email) WHERE IsDeleted = 0;
GO
