# SuperHero Registry

A full-stack web application to register, search, update and retire superheroes.
It is built with **.NET 10 (ASP.NET Core Web API)** using **Clean Architecture** and **Test-Driven Development**, a **SQL Server** database accessed through a custom **Reflection-based data layer** (no Entity Framework, Dapper or MediatR), and an **Angular** front end.

> **Status:** in development. Work is tracked in the [`Scrum`](Scrum/) folder. See the user story [US-001](Scrum/US-001-SuperHero-Registry.md) and its [tasks](Scrum/Tasks/).

---

## Table of contents

1. [Features](#features)
2. [Tech stack](#tech-stack)
3. [Architecture](#architecture)
4. [Solution structure](#solution-structure)
5. [Data model](#data-model)
6. [API endpoints](#api-endpoints)
7. [Getting started](#getting-started)
   - [Prerequisites](#prerequisites)
   - [Run for development (.NET Aspire)](#run-for-development-net-aspire)
   - [Run for delivery (Docker Compose)](#run-for-delivery-docker-compose)
   - [Demo credentials](#demo-credentials)
8. [Logging and telemetry (Application Insights)](#logging-and-telemetry-application-insights)
9. [Testing](#testing)
10. [Design decisions](#design-decisions)
11. [Generative AI usage](#generative-ai-usage)
12. [Project management](#project-management)

---

## Features

- **User accounts:** register, log in with a JWT, view your own profile, and update your user name and email.
- **Hero CRUD:** create, list (with search, sorting and paging), view, edit and delete superhero records.
- **Role-based authorization:**
  - *Anonymous:* register, log in, health check.
  - *Authenticated user:* read, create and update heroes, and update **their own** account.
  - *Administrator:* everything above, plus deleting heroes, and managing users (list, update **any** account, delete).
- **Soft delete:** deleted records are flagged with `IsDeleted` and never physically removed.
- **Audit trail:** every record stores `CreatedBy`, `CreatedAt`, `UpdatedBy` and `UpdatedAt`.
- **Business validation:** unique hero name and email, a power level from 1 to 100, a valid US address, a birth date in the past, and more.
- **Responsive UI:** the Angular SPA works on desktop and mobile.
- **Seeded demo data:** an administrator account and three heroes (Captain America, Spider-Man and Superman) are created on first start.

## Tech stack

| Layer            | Technology                                                                                   |
| ---------------- | -------------------------------------------------------------------------------------------- |
| Front end        | Angular 22, TypeScript, SCSS, Angular Router, Reactive Forms, Signals, Vitest                |
| API              | ASP.NET Core 10 Web API (controllers), JWT Bearer authentication, OpenAPI with the Scalar API reference |
| Application      | Use-case services, validators and DTOs (plain C#, no MediatR)                                 |
| Domain           | Entities, value objects and domain rules (no framework dependencies)                         |
| Data access      | ADO.NET (`Microsoft.Data.SqlClient`) plus a custom **Reflection-based object mapper**        |
| Database         | SQL Server 2022                                                                              |
| Dev orchestration| .NET Aspire (AppHost and ServiceDefaults: SQL Server, API and Angular with one command)      |
| Observability    | OpenTelemetry → Aspire dashboard and **Azure Application Insights** (requests, SQL dependencies, structured logs) |
| Delivery         | Docker and Docker Compose                                                                    |
| Testing          | xUnit v3 (Microsoft.Testing.Platform), NSubstitute, Shouldly, Testcontainers (SQL Server), `WebApplicationFactory`, Vitest |
| CI               | GitHub Actions                                                                               |

## Architecture

The solution follows **Clean Architecture**: dependencies point inward, and the Domain has no dependencies at all.

```
            ┌─────────────────────────────────────────────┐
            │                 Presentation                │
            │  HeroRegistry.Api (Controllers, Auth, DI)   │
            │  hero-registry-ui (Angular SPA)             │
            └───────────────┬─────────────────────────────┘
                            │ depends on
            ┌───────────────▼─────────────────────────────┐
            │                Application                  │
            │  Use-case services, DTOs, validators,       │
            │  ports (IHeroRepository, IUserRepository,   │
            │  IPasswordHasher, ITokenService, IClock,    │
            │  ICurrentUser)                              │
            └───────────────┬─────────────────────────────┘
                            │ depends on
            ┌───────────────▼─────────────────────────────┐
            │                   Domain                    │
            │  Hero, User, AuditableEntity, value objects,│
            │  domain exceptions and invariants           │
            └─────────────────────────────────────────────┘
                            ▲
                            │ implements Application ports
            ┌───────────────┴─────────────────────────────┐
            │               Infrastructure                │
            │  Reflection data mapper, SQL repositories,  │
            │  migrations and seeding, BCrypt hasher,     │
            │  JWT token service                          │
            └─────────────────────────────────────────────┘
```

### Reflection-based data layer

Entity Framework, Dapper and MediatR are deliberately **not used**. Persistence is handled by a small in-house mapper in `HeroRegistry.Infrastructure/Persistence`:

- **A fluent `EntityMap<T>`** describes the mapping in Infrastructure, so the domain entities carry no attributes. `SuperHeroMap` and `UserMap` (deriving from `AuditableEntityMap<T>`) map the entities **directly** to their tables:
  ```csharp
  ToTable("SuperHeroes");
  Property(h => h.Superpowers).StoredAsJson();
  Property(h => h.State).IsAnsi().IsFixedLength().HasMaxLength(2);
  ```
  Conventions:
  - every property with a setter is a column;
  - `Id` is the identity key;
  - `CreatedBy` and `CreatedAt` are insert-only;
  - `IsDeleted` is the soft-delete flag.
- **`EntityMetadata`** reads the entity's properties, private setters and private constructor once through Reflection, and caches them per map. It validates table and column names as plain identifiers, gives each column an explicit `DbType` and size (for example `NVARCHAR(100)`, `CHAR(2)`, `DATE`), and reads nullability from the C# declarations.
- **`SqlStatementBuilder`** generates **parameterized** `SELECT`, `INSERT ... OUTPUT INSERTED.[Id]`, `UPDATE` and soft-`DELETE` statements. Identifiers come only from the metadata and are bracket-quoted. Every value is a typed parameter, so there is no SQL injection surface. Reads and updates skip soft-deleted rows, and updates never touch the creation audit.
- **`DataRecordMapper<T>`** hydrates entities from a data reader. It resolves column positions once per reader, calls the private setters, and handles `DBNull`, nullable types, enums, `DateOnly`, UTC `DateTime` and JSON columns such as `Superpowers`.
- **`SqlRepository<T>`** is a generic base repository: `GetByIdAsync`, `ListAsync`, `InsertAsync`, `UpdateAsync` and `SoftDeleteAsync`, plus `QueryAsync` and `ExecuteScalarAsync` for custom queries. Each operation is timed, traced and logged (see [Logging and telemetry](#logging-and-telemetry-application-insights)). `HeroRepository` and `UserRepository` (T06) extend it with search, paging and uniqueness checks.
- It is built on the ADO.NET base classes (`DbConnection`, `DbCommand`, `DbParameter`), so only `SqlConnectionFactory` knows the provider (`Microsoft.Data.SqlClient`).

## Solution structure

```
hero-registry/
├── HeroRegistry/                       # .NET solution
│   ├── HeroRegistry.slnx
│   ├── src/
│   │   ├── HeroRegistry.Domain/
│   │   ├── HeroRegistry.Application/
│   │   ├── HeroRegistry.Infrastructure/
│   │   │   └── Persistence/Scripts/    # versioned SQL migration and seed scripts
│   │   ├── HeroRegistry.Api/
│   │   ├── HeroRegistry.AppHost/       # .NET Aspire orchestrator (development)
│   │   └── HeroRegistry.ServiceDefaults/
│   └── tests/
│       ├── HeroRegistry.Domain.Tests/
│       ├── HeroRegistry.Application.Tests/
│       ├── HeroRegistry.Infrastructure.Tests/   # Testcontainers SQL Server
│       └── HeroRegistry.Api.Tests/              # WebApplicationFactory
├── hero-registry-ui/                   # Angular SPA
├── Scrum/                              # User story and tasks
├── docker-compose.yml                  # Delivery
└── .github/workflows/                  # CI
```

## Data model

### `Users`

| Column      | Type            | Notes                                         |
| ----------- | --------------- | --------------------------------------------- |
| Id          | INT IDENTITY PK |                                               |
| UserName    | NVARCHAR(50)    | Required, unique among active users           |
| Email       | NVARCHAR(256)   | Required, unique among active users           |
| Password    | NVARCHAR(200)   | **BCrypt hash.** Plain text is never stored   |
| IsAdmin     | BIT             | Default `0`                                   |
| IsDeleted   | BIT             | Default `0` (soft delete)                     |
| CreatedBy   | INT NULL        | FK to `Users.Id` (NULL for seed or self-registration) |
| CreatedAt   | DATETIME2       | UTC                                           |
| UpdatedBy   | INT NULL        | FK to `Users.Id`                              |
| UpdatedAt   | DATETIME2 NULL  | UTC                                           |

### `SuperHeroes`

| Column      | Type            | Notes                                              |
| ----------- | --------------- | -------------------------------------------------- |
| Id          | INT IDENTITY PK |                                                    |
| HeroName    | NVARCHAR(100)   | Required, unique among active heroes               |
| FirstName   | NVARCHAR(100)   | Required                                           |
| LastName    | NVARCHAR(100)   | Required                                           |
| BirthDate   | DATE            | Required, must be in the past                      |
| Email       | NVARCHAR(256)   | Required, valid format, unique among active heroes |
| Superpowers | NVARCHAR(MAX)   | JSON array of strings (`ISJSON` check), 1 to 10 items |
| PowerLevel  | INT             | 1 to 100 (`CHECK` constraint)                      |
| Address1    | NVARCHAR(200)   | Required                                           |
| Address2    | NVARCHAR(200)   | Optional                                           |
| City        | NVARCHAR(100)   | Required                                           |
| State       | CHAR(2)         | US state code                                      |
| ZipCode     | VARCHAR(10)     | `12345` or `12345-6789`                            |
| IsDeleted   | BIT             | Default `0` (soft delete)                          |
| CreatedBy   | INT             | FK to `Users.Id`                                   |
| CreatedAt   | DATETIME2       | UTC                                                |
| UpdatedBy   | INT NULL        | FK to `Users.Id`                                   |
| UpdatedAt   | DATETIME2 NULL  | UTC                                                |

Uniqueness (user name, hero name and both emails) ignores case and applies to **active** rows only. A soft-deleted row frees its values. These rules are enforced in the business layer and again by the database as a last line of defense (filtered unique indexes and CHECK constraints).

### Schema migrations

The schema and the demo data are created by versioned SQL scripts in `HeroRegistry/src/HeroRegistry.Infrastructure/Persistence/Scripts/`: `0001_CreateUsers.sql`, `0002_CreateSuperHeroes.sql` and `0003_SeedData.sql` (the administrator and three heroes). The API applies them **automatically when it starts**, under both Aspire and Docker Compose, through a small in-house runner, because Entity Framework migrations are not used. The runner:

- creates the database if it doesn't exist;
- records each applied script and its checksum in `dbo.SchemaVersions`;
- applies each pending script in its own transaction (all or nothing);
- retries while SQL Server is still starting;
- serializes concurrent startups;
- refuses to start if an already applied script was edited.

To change the schema, **add a new numbered script**. Never edit an applied one.

| Setting                          | Default    | Purpose |
| -------------------------------- | ---------- | ------- |
| `Database:MigrateOnStartup`      | `true`     | Apply pending scripts when the API starts |
| `Database:MigrationMaxAttempts`  | `10`       | Attempts while SQL Server is not reachable yet |
| `Database:MigrationRetryDelay`   | `00:00:03` | Wait between attempts |

## API endpoints

In Development, the API publishes an interactive **Scalar API reference** at `/scalar`, generated from the OpenAPI document at `/openapi/v1.json`. The API's base URL (`/`) redirects to it. Starting the API from Visual Studio or Rider (F5), or with `dotnet watch`, opens it automatically. In the Aspire dashboard, use the **Scalar API Reference** link on the `api` resource. Neither endpoint is exposed outside Development.

### Auth and Users API

| Verb   | Route                 | Auth      | Success | Description                    |
| ------ | --------------------- | --------- | ------- | ------------------------------ |
| POST   | `/api/auth/register`  | Anonymous | 201     | Create a regular user account (400 invalid, 409 taken) |
| POST   | `/api/auth/login`     | Anonymous | 200     | Return a JWT and user info     |
| GET    | `/api/auth/me`        | User      | 200     | Current user's profile         |
| GET    | `/api/users`          | Admin     | 200     | List users                     |
| PUT    | `/api/users/{id}`     | Owner or Admin | 200 | Update user name and email. Users can update only themselves; admins can update anyone (403 otherwise) |
| DELETE | `/api/users/{id}`     | Admin     | 204     | Soft-delete a user             |

Send the token from login as `Authorization: Bearer <accessToken>`. Every endpoint requires a signed-in user unless marked Anonymous. An anonymous request to any other URL, even an unknown one, gets 401, so routes can't be probed.

Errors are `application/problem+json` with a stable `code` (for example `user.duplicate` or `auth.invalid_credentials`) and, for 400 and 409, an `errors` object keyed by field (`userName`, `email`, ...).

### Heroes API

| Verb   | Route                | Auth  | Success | Errors             | Description                                        |
| ------ | -------------------- | ----- | ------- | ------------------ | -------------------------------------------------- |
| GET    | `/api/heroes`        | User  | 200     | 400, 401           | Paged list (`search`, `page`, `pageSize`, `sortBy`, `sortDirection`) |
| GET    | `/api/heroes/{id}`   | User  | 200     | 401, 404           | Hero details, with the audit trail                 |
| POST   | `/api/heroes`        | User  | 201     | 400, 401, 409      | Create a hero (returns a `Location` header)        |
| PUT    | `/api/heroes/{id}`   | User  | 200     | 400, 401, 404, 409 | Update a hero (replaces every editable field)      |
| DELETE | `/api/heroes/{id}`   | Admin | 204     | 401, 403, 404      | Soft-delete a hero                                 |

List parameters:
- `search` matches hero name, first name, last name, city or any superpower (contains, ignoring case).
- `sortBy` is `HeroName` (default), `PowerLevel` or `CreatedAt`.
- `sortDirection` is `Ascending` (default) or `Descending`.
- `page` (default 1) and `pageSize` (default 10, maximum 100) are normalized when out of range.

For example: `GET /api/heroes?search=flight&sortBy=PowerLevel&sortDirection=Descending&page=1&pageSize=10`. The response is `{ items, page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage }`.

Dates use `yyyy-MM-dd` (`"birthDate": "1977-06-18"`), and `superpowers` is a JSON array of strings.

### Other endpoints

| Verb | Route      | Auth      | Description  |
| ---- | ---------- | --------- | ------------ |
| GET  | `/health`  | Anonymous | Readiness: the API and SQL Server are healthy. Available in every environment for container probes; the body is only `Healthy`, `Degraded` or `Unhealthy` |
| GET  | `/alive`   | Anonymous | Liveness: the API process is responsive. Available in every environment |

### Error format

**Every** error response is RFC 9457 problem details (`application/problem+json`). That includes use-case errors, authentication and authorization (401 and 403), unknown routes, model binding and malformed JSON, and unhandled exceptions:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more fields are invalid.",
  "status": 400,
  "code": "validation.failed",
  "errors": { "powerLevel": ["PowerLevel must be between 1 and 100."] },
  "traceId": "00-925b0098b63dd703e91fa7ffcc5d92b8-b70a3ab4d05fab9c-01"
}
```

- `code` is stable and machine-readable, for example `validation.failed`, `auth.unauthenticated`, `auth.forbidden`, `auth.invalid_credentials`, `hero.not_found`, `hero.duplicate`, `user.duplicate` or `server.error`.
- `errors` (400 and 409) is keyed by camelCase field name.
- `traceId` contains the trace id, which is the `operation_Id` to search in Application Insights.
- Unhandled exceptions return a generic 500 (`"An unexpected error occurred."`) with no message, type or stack trace. Code bugs are logged with their full exception. Database exceptions are logged only by type and SQL error number, because their messages can contain personal data.

### CORS

Browsers may call the API only from the origins in `Cors:AllowedOrigins`: by default `http://localhost:4200` (Angular dev server) and `http://localhost:8080` (Docker Compose UI). Allowed are any header, the verbs `GET`, `POST`, `PUT` and `DELETE`, the exposed `Location` header, and **no credentials**. Invalid entries (a wildcard, a path, a trailing slash) stop the API at startup. When the UI is served through a proxy on the API's origin (the Aspire dev proxy, nginx), CORS isn't even involved.

## Getting started

### Prerequisites

| Tool                         | Needed for                    |
| ---------------------------- | ----------------------------- |
| .NET SDK 10                  | Back end and Aspire           |
| Node.js 22 LTS or later and npm | Angular UI                 |
| Docker Desktop               | SQL Server container, Aspire and Compose |
| Aspire CLI (optional)        | Not required. The AppHost uses the orchestrator and dashboard from NuGet |

### Run for development (.NET Aspire)

The Aspire AppHost starts SQL Server in a container, the API and the Angular dev server, and wires up their connection strings and URLs. Docker Desktop must be running.

```bash
dotnet run --project HeroRegistry/src/HeroRegistry.AppHost
```

In Visual Studio, set `HeroRegistry.AppHost` as the startup project and press F5.

| Resource         | URL / address                               | Notes |
| ---------------- | ------------------------------------------- | ----- |
| Aspire dashboard | https://localhost:17180 (http://localhost:15180 with `--launch-profile http`) | The login URL with a token is printed in the console |
| API              | http://localhost:5103                       | `/scalar` (API reference, also the dashboard link), `/health` (includes SQL Server), `/alive`, `/openapi/v1.json` |
| Angular UI       | http://localhost:4200                       | `/api/*` is proxied to the API (`hero-registry-ui/proxy.conf.mjs`) |
| SQL Server       | Dynamic port, shown in the dashboard        | Container `sql`, database `HeroRegistryDb` |

How it works:

- **npm packages** are installed automatically before the UI starts.
- **Secrets are generated on first run** and stored in the AppHost's user secrets, never in source control. These are the SQL Server SA password (`Parameters:sql-password`) and a random 64-character JWT signing key (`Parameters:jwt-key`, passed to the API as `Jwt__Key`). To view them, run `dotnet user-secrets list --project HeroRegistry/src/HeroRegistry.AppHost`.
- **The API refuses to start without a JWT signing key** (`Jwt:Key`, at least 32 bytes). Under Aspire it is provided automatically. To run the API on its own (for example F5 on the API project), store one in the API's user secrets once:
  ```bash
  dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>" --project HeroRegistry/src/HeroRegistry.Api
  ```
- **SQL Server keeps running** after you stop the AppHost (persistent lifetime), and its data lives in the `hero-registry-sql-data` volume. Restarts are fast, and data survives them. To reset everything, run `docker rm -f` on the `sql-*` container, then `docker volume rm hero-registry-sql-data`.
- **Without Aspire**, you can run the API (`dotnet run --project HeroRegistry/src/HeroRegistry.Api`) and the UI (`npm start` in `hero-registry-ui`) separately. In that case, provide `ConnectionStrings__HeroRegistryDb` yourself.

The database schema and the demo seed data are applied automatically when the API starts (see [Schema migrations](#schema-migrations)).

### Run for delivery (Docker Compose)

```bash
cp .env.example .env          # then set SA_PASSWORD and JWT__KEY
docker compose up --build -d
```

| Service      | URL                       |
| ------------ | ------------------------- |
| Angular UI   | http://localhost:8080     |
| API          | http://localhost:5080     |
| SQL Server   | `localhost,14333`         |

To stop the stack: `docker compose down`. To also remove the database volume: `docker compose down -v`.

### Demo credentials

| Role          | User name | Password | Email                    |
| ------------- | --------- | -------- | ------------------------ |
| Administrator | `Admin`   | `Admin`  | `admin@heroregistry.com` |

To try the regular-user experience (for example, a regular user can't delete heroes), register a new account from the login page.

This account is for demo purposes only. Its password is weaker than the registration policy allows, and it is stored in `0003_SeedData.sql` as a BCrypt hash, never as plain text. For any shared or real deployment, replace it (with a new numbered script containing a new hash) and change the JWT key.

**Seeded heroes:** Captain America, Spider-Man and Superman.

## Logging and telemetry (Application Insights)

The API's telemetry goes through one OpenTelemetry pipeline and can be sent to two destinations at the same time: the **Aspire dashboard** (development) and **Azure Application Insights**.

| What is collected            | Source                                                     | Application Insights table |
| ---------------------------- | ---------------------------------------------------------- | -------------------------- |
| Requests to endpoints        | ASP.NET Core instrumentation (health probes are excluded)  | `requests`                 |
| SQL commands                 | SqlClient instrumentation (client-side)                    | `dependencies` (type SQL)  |
| Data-layer operations, timed | `DataAccessInstrumentation` (`HeroRegistry.Data` spans)    | `dependencies`             |
| Structured logs              | `ILogger`, including `DataAccessLog` (inserts, selects, updates, soft deletes, slow queries, failures) | `traces` |

### Enable it

Application Insights is **on only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set**. Without it, the API, the tests and CI run normally.

```bash
# Development (user secrets of the API project; also used when running under Aspire)
dotnet user-secrets set APPLICATIONINSIGHTS_CONNECTION_STRING "InstrumentationKey=...;IngestionEndpoint=..." --project HeroRegistry/src/HeroRegistry.Api

# Docker Compose: set APPLICATIONINSIGHTS_CONNECTION_STRING in .env (T15)
```

The connection string must come from the Azure portal. A placeholder value such as `<use secrets>` stops the API at startup with a clear error, instead of silently losing telemetry.

| Setting                                         | Default | Purpose |
| ----------------------------------------------- | ------- | ------- |
| `ApplicationInsights:SamplingRatio`             | `1.0`   | Fraction of traces sent (0.0 to 1.0) |
| `Database:SlowCommandThresholdMs`               | `500`   | Data operations slower than this also log a Warning (0 disables it) |
| `Logging:OpenTelemetry:LogLevel:<category>`     | see `appsettings.json` | Log filtering for the telemetry pipeline. Data-layer logs (`HeroRegistry.Infrastructure.Persistence`) are `Information` (reads at `Debug` in Development) |

### Privacy and SQL Server

- **Nothing is installed or changed on SQL Server.** All tracking happens client-side, in the API process.
- **No personal data in telemetry:** SQL parameter values are never captured, literals in SQL text are replaced by `?`, and exception messages (which SQL Server can fill with data, for example duplicate key values) are never attached. Only tables, ids, row counts, timings and error types are recorded.
- **Health-check queries are not traced**, so the `/health` polling doesn't add cost or noise.
- **Tests never send telemetry to Azure**, even though Development loads the user secrets.

### Useful queries (KQL)

```kusto
// Requests per endpoint
requests
| summarize count(), avg(duration), percentile(duration, 95) by name, resultCode

// SQL commands with their (parameterized) text
dependencies
| where type == "SQL"
| project timestamp, target, data, duration, success, operation_Id

// Data-layer operations: inserts, updates, soft deletes, slow queries and failures
traces
| where customDimensions.CategoryName startswith "HeroRegistry.Infrastructure.Persistence"
| project timestamp, severityLevel, message, customDimensions.Table, customDimensions.ElapsedMs
```

The `operation_Id` correlates a request with its SQL dependencies and logs in the Application Insights transaction view.

## Testing

The project is developed with **TDD** (red, green, refactor).

```bash
# Back end: all test projects
dotnet test --solution HeroRegistry/HeroRegistry.slnx

# Back end with coverage (Cobertura report in each project's TestResults folder)
dotnet test --solution HeroRegistry/HeroRegistry.slnx --coverage --coverage-output-format cobertura

# Front end
cd hero-registry-ui && npm test
```

| Test project                | Scope                                                                   |
| --------------------------- | ----------------------------------------------------------------------- |
| `Domain.Tests`              | Entity invariants and value objects                                     |
| `Application.Tests`         | Use cases and validators, using mocked ports                            |
| `Infrastructure.Tests`      | Reflection mapper (unit) and repositories against real SQL Server (Testcontainers) |
| `Api.Tests`                 | Endpoints end to end: HTTP verbs, status codes, auth and ProblemDetails |
| Angular specs               | Services, guards, interceptors and components                           |

Docker must be running for the Infrastructure tests.

The test projects use **xUnit v3** on **Microsoft.Testing.Platform**. The `global.json` at the repository root opts `dotnet test` into that platform. This is why the command takes `--solution`.

## Design decisions

| Decision | Rationale |
| -------- | --------- |
| Clean Architecture with 4 projects | Business rules are independent of the database, the web framework and the UI, so they can be tested in isolation. |
| Reflection-based mapper instead of an ORM | Meets the "no EF, Dapper or MediatR" constraint while avoiding hand-written mapping code for every entity. Metadata is cached per type, so Reflection runs only once. |
| Parameterized SQL only | No string-concatenated values, which prevents SQL injection. |
| Superpowers stored as a JSON column | Keeps the generic mapper simple (one row per entity) while still allowing `OPENJSON` queries. |
| Soft delete and audit columns | Preserves history and traceability. Uniqueness is enforced with **filtered unique indexes** (`WHERE IsDeleted = 0`). |
| BCrypt password hashing | Uses an adaptive work factor. The `Password` column only ever stores a hash. |
| JWT Bearer authentication | Stateless API that fits an SPA client. The `IsAdmin` flag maps to an `Admin` role claim. |
| Plain application services instead of MediatR | Keeps the code explicit and easy to follow, and meets the constraint. |
| .NET Aspire for development | One command runs the whole system, with built-in observability. |
| Docker Compose for delivery | A reproducible, portable deployment with no SDK required. |

## Generative AI usage

AI-assisted development (Claude Code) is used throughout the project. The prompts, a representative sample of the generated code, how the suggestions were validated, and what was corrected are documented in [`docs/genai.md`](docs/genai.md). That file also covers the task-management API exercise.

## Project management

- User story: [US-001 SuperHero Registry](Scrum/US-001-SuperHero-Registry.md)
- Task breakdown: [Scrum/Tasks](Scrum/Tasks/)
