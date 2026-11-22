# T03: Database schema and migration runner

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Infrastructure · **Estimate:** 4 h · **Depends on:** T01, T02 · **Status:** Done

## Goal
Create the SQL Server schema (`Users`, `SuperHeroes`) with versioned scripts that the API applies automatically at startup, under both Aspire (development) and Docker Compose (delivery). Entity Framework migrations are not allowed.

> **Scope change:** the seed data (Admin and the three heroes) moved to [T03.1](T03.1-Seed-Data.md). The seeder needs the domain model (T04), the repositories (T06) and the password hasher (T08), which come later in the plan.

## Steps
- [x] Embedded SQL scripts under `Infrastructure/Persistence/Scripts/`, with the stable resource names `Scripts/0001_CreateUsers.sql` and `Scripts/0002_CreateSuperHeroes.sql`.
- [x] `Users` table:
  - `Id INT IDENTITY PK`, `UserName NVARCHAR(50)`, `Email NVARCHAR(256)`, `Password NVARCHAR(200)` (hash), `IsAdmin BIT DEFAULT 0`, `IsDeleted BIT DEFAULT 0`
  - `CreatedBy INT NULL FK`, `CreatedAt DATETIME2(3) DEFAULT SYSUTCDATETIME()`, `UpdatedBy INT NULL FK`, `UpdatedAt DATETIME2(3) NULL`
  - Filtered unique indexes on `UserName` and `Email` `WHERE IsDeleted = 0`
  - `UserName` and `Email` use an explicit **case-insensitive** collation, so uniqueness ignores case whatever the server default is.
- [x] `SuperHeroes` table:
  - All fields from the user story.
  - CHECK constraints as a last line of defense:
    - `Superpowers` must be a JSON **array** (`ISJSON(Superpowers, ARRAY)`, SQL Server 2022).
    - `PowerLevel BETWEEN 1 AND 100`.
    - `State` must be two uppercase letters.
    - `ZipCode` must be `12345` or `12345-6789`.
  - `CreatedBy INT NOT NULL FK Users(Id)` and `UpdatedBy INT NULL FK Users(Id)`.
  - Filtered unique indexes on `HeroName` and `Email` `WHERE IsDeleted = 0`, case-insensitive.
  - Index on `(IsDeleted, HeroName)` for listing.
- [x] `DatabaseMigrator` (Infrastructure, `IDatabaseMigrator`):
  - Creates the database if it is missing (`master` connection, `QUOTENAME` for a safe dynamic `CREATE DATABASE`).
  - Creates `dbo.SchemaVersions` (`ScriptName`, `Checksum` SHA-256, `AppliedAt`).
  - Runs pending scripts in order. Batches are split on `GO` lines. Each script runs in its own transaction with `XACT_ABORT ON`, so it is applied completely or not at all.
  - Is idempotent: already applied scripts are skipped.
  - Refuses to start if an applied script was **edited** (checksum mismatch). Line endings are normalized, so CRLF and LF checkouts compare equal.
  - Serializes concurrent runs (several API instances starting at once) with `sp_getapplock`.
- [x] Startup hook: `DatabaseInitializer` (`IHostedService`, registered by `AddInfrastructure`) runs the migrator before the API serves requests.
  - Retries database errors while SQL Server is starting: `Database:MigrationMaxAttempts` (default 10) and `Database:MigrationRetryDelay` (default 3 s). After the last attempt the error stops the application, instead of serving requests against a missing schema.
  - Does not retry other errors, such as an edited script, which a retry cannot fix.
  - Skipped with a warning when no connection string is configured (for example, the API integration tests). Can be turned off with `Database:MigrateOnStartup = false`.
- [x] `DatabaseOptions` is now bound from the `Database` configuration section, with validation.
- [x] Aspire: **no change needed**. T02 already provides the SQL Server container, the `HeroRegistryDb` database and the connection string. The API applies the migrations itself when it starts.

## Tests (TDD)
- [x] `MigrationScriptsTests` and `SqlBatchSplitterTests` (unit):
  - Scripts are loaded in order, each with a checksum.
  - The checksum ignores line endings but detects edits.
  - `GO` splitting: case, spaces, `GO` inside words or strings, empty batches.
- [x] `DatabaseMigratorTests` (Testcontainers, a fresh database per test):
  - Creates the database and the tables.
  - Records the scripts in order.
  - Running again is a no-op.
  - **Concurrent startups apply each script exactly once.**
  - **An edited applied script is rejected.**
  - **A failing script is rolled back and not recorded.**
  - Rejects a connection string without a database name.
- [x] `SchemaConstraintsTests` (Testcontainers):
  - Valid insert and defaults.
  - Each CHECK constraint, including boundaries.
  - The hero must be created by an existing user (foreign key).
  - Case-insensitive uniqueness for hero name and email, and for user name and email.
  - A soft-deleted row frees its name and email.
  - A new user is not an admin by default.
- [x] `DatabaseInitializerTests` (NSubstitute, FakeLogger):
  - Runs on startup.
  - Retries database errors, then succeeds.
  - Stops after the last attempt.
  - Does not retry non-database errors.
  - Skips without a connection string or when disabled.
- [x] DI tests: `Database` section binding and defaults, validation, and that the migrator and hosted service are registered.
- [x] Red checks: removing the checksum guard fails only the edited-script test, and removing the application lock fails only the concurrency test.

## Done when
- [x] A fresh database ends up with the schema after the API starts. Verified under Aspire: `/health` returned `Healthy`, `SchemaVersions` listed both scripts, and the `Users`, `SuperHeroes` and `SchemaVersions` tables and all indexes exist in `HeroRegistryDb`.
- [x] Restarts do not reapply scripts (idempotency tests).
- [x] The build has 0 warnings, and all tests pass (156).

## Implementation notes
- **Adding a schema change later:** add `0003_<Description>.sql` to `Persistence/Scripts/`. It is embedded automatically (wildcard in the `.csproj`). **Never edit an applied script.** The migrator refuses to start when it detects one.
- **SQL Server version:** `ISJSON(..., ARRAY)` requires SQL Server 2022 (compatibility level 160), which is what Aspire, Testcontainers and Docker Compose use.
- **Telemetry:** the migration SQL is traced like any other command (T02.2). It contains only DDL, no data.
