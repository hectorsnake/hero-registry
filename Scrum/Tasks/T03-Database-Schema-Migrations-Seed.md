# T03: Database schema, migration runner and seed data

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Infrastructure · **Estimate:** 4 h · **Depends on:** T01

## Goal
Create the SQL Server schema (`Users`, `SuperHeroes`) with versioned scripts that are applied automatically at startup. Entity Framework migrations are not allowed.

## Steps
- [ ] Add embedded SQL scripts under `Infrastructure/Persistence/Scripts/`:
  - `0001_CreateUsers.sql`
  - `0002_CreateSuperHeroes.sql`
  - `0003_SeedData.sql` (or seed through code, see below)
- [ ] `Users` table:
  - `Id INT IDENTITY PK`, `UserName NVARCHAR(50)`, `Email NVARCHAR(256)`, `Password NVARCHAR(200)` (hash), `IsAdmin BIT DEFAULT 0`, `IsDeleted BIT DEFAULT 0`
  - `CreatedBy INT NULL FK`, `CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()`, `UpdatedBy INT NULL FK`, `UpdatedAt DATETIME2 NULL`
  - Filtered unique indexes on `UserName` and `Email` `WHERE IsDeleted = 0`
- [ ] `SuperHeroes` table:
  - All fields from the user story. `Superpowers NVARCHAR(MAX)` with `CHECK (ISJSON(Superpowers) = 1)`. `PowerLevel INT CHECK (PowerLevel BETWEEN 1 AND 100)`. `State CHAR(2)`, `ZipCode VARCHAR(10)`.
  - `CreatedBy INT NOT NULL FK Users(Id)` and `UpdatedBy INT NULL FK Users(Id)`
  - Filtered unique indexes on `HeroName` and `Email` `WHERE IsDeleted = 0`
  - Index on `(IsDeleted, HeroName)` for listing
- [ ] Add `DatabaseMigrator` (Infrastructure):
  - Creates the database if it is missing (`master` connection).
  - Creates a `SchemaVersions` table.
  - Runs pending embedded scripts in order, each inside a transaction.
  - Is idempotent.
- [ ] Add `DatabaseSeeder`:
  - If no users exist, insert `admin` (IsAdmin) and `demo` with **BCrypt-hashed** passwords (uses the T08 hasher, through the repository).
  - Insert at least 10 original sample heroes created by `admin`.
- [ ] Add a hosted service or startup hook in the API that runs the migrator and seeder, with retry until SQL Server is ready (Polly or a simple backoff loop).

## Tests (TDD)
- [ ] Integration (Testcontainers SQL Server):
  - Running the migrator on an empty server creates both tables.
  - Running it twice is a no-op.
  - The seeder inserts users and heroes only once.
- [ ] Constraint tests:
  - Inserting `PowerLevel = 0`, invalid JSON or a duplicate active `HeroName` fails.
  - A duplicate name of a **deleted** hero is allowed.

## Done when
- A fresh container ends up with the schema and seed data after the API starts, and restarts do not duplicate data.
