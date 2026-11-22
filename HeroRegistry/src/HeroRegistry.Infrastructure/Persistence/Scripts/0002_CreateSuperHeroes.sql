-- 0002: SuperHeroes table (the registry itself).
-- Applied once by DatabaseMigrator. Never edit an applied script: add a new numbered script instead.
-- The CHECK constraints are a last line of defense. The business rules are enforced first by the
-- Domain and Application layers (T04, T07).

CREATE TABLE dbo.SuperHeroes
(
    Id          INT IDENTITY(1, 1)  NOT NULL CONSTRAINT PK_SuperHeroes PRIMARY KEY,
    -- Case-insensitive collation, so "Superman" and "superman" are the same hero name.
    HeroName    NVARCHAR(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    FirstName   NVARCHAR(100) NOT NULL,
    LastName    NVARCHAR(100) NOT NULL,
    BirthDate   DATE          NOT NULL,
    Email       NVARCHAR(256) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    -- JSON array of strings, for example ["Flight","Heat vision"]. ISJSON(..., ARRAY) needs SQL Server 2022.
    Superpowers NVARCHAR(MAX) NOT NULL
        CONSTRAINT CK_SuperHeroes_Superpowers_IsJsonArray CHECK (ISJSON(Superpowers, ARRAY) = 1),
    PowerLevel  INT           NOT NULL
        CONSTRAINT CK_SuperHeroes_PowerLevel CHECK (PowerLevel BETWEEN 1 AND 100),
    Address1    NVARCHAR(200) NOT NULL,
    Address2    NVARCHAR(200) NULL,
    City        NVARCHAR(100) NOT NULL,
    -- Two uppercase letters (US state code). The binary collation makes the check case-sensitive.
    State       CHAR(2)       NOT NULL
        CONSTRAINT CK_SuperHeroes_State CHECK (State COLLATE Latin1_General_BIN LIKE '[A-Z][A-Z]'),
    -- 12345 or 12345-6789
    ZipCode     VARCHAR(10)   NOT NULL
        CONSTRAINT CK_SuperHeroes_ZipCode CHECK (
            ZipCode LIKE '[0-9][0-9][0-9][0-9][0-9]'
            OR ZipCode LIKE '[0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_SuperHeroes_IsDeleted DEFAULT (0),
    CreatedBy   INT           NOT NULL CONSTRAINT FK_SuperHeroes_CreatedBy REFERENCES dbo.Users (Id),
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_SuperHeroes_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedBy   INT           NULL     CONSTRAINT FK_SuperHeroes_UpdatedBy REFERENCES dbo.Users (Id),
    UpdatedAt   DATETIME2(3)  NULL
);
GO

-- Unique among active heroes only: a soft-deleted hero frees its name and email.
CREATE UNIQUE INDEX UX_SuperHeroes_HeroName ON dbo.SuperHeroes (HeroName) WHERE IsDeleted = 0;
GO

CREATE UNIQUE INDEX UX_SuperHeroes_Email ON dbo.SuperHeroes (Email) WHERE IsDeleted = 0;
GO

-- Default listing: active heroes ordered by name.
CREATE INDEX IX_SuperHeroes_IsDeleted_HeroName ON dbo.SuperHeroes (IsDeleted, HeroName);
GO
