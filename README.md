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

- **User accounts:** register, log in with a JWT, and view your own profile.
- **Hero CRUD:** create, list (with search, sorting and paging), view, edit and delete superhero records.
- **Role-based authorization:**
  - *Anonymous:* register, log in, health check.
  - *Authenticated user:* read, create and update heroes.
  - *Administrator:* everything above, plus deleting heroes and managing users.
- **Soft delete:** deleted records are flagged with `IsDeleted` and never physically removed.
- **Audit trail:** every record stores `CreatedBy`, `CreatedAt`, `UpdatedBy` and `UpdatedAt`.
- **Business validation:** unique hero name and email, a power level from 1 to 100, a valid US address, a birth date in the past, and more.
- **Responsive UI:** the Angular SPA works on desktop and mobile.
- **Seeded demo data:** demo users and sample heroes are created on first start.

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

Entity Framework, Dapper and MediatR are deliberately **not used**. Persistence is handled by a small in-house mapper:

- **Attributes** describe the mapping: `[Table]`, `[Key]`, `[Column]`, `[NotMapped]`, `[JsonColumn]` and `[SoftDelete]`.
- **`EntityMetadata<T>`** reads the entity's properties once through Reflection and caches them per type.
- **`SqlStatementBuilder`** generates **parameterized** `SELECT`, `INSERT`, `UPDATE` and soft-`DELETE` statements. Column names come only from the cached metadata and are bracket-quoted. Values are always passed as `SqlParameter`s, so there is no SQL injection surface.
- **`DataRecordMapper`** hydrates objects from an `IDataReader`. It handles `DBNull`, nullable types, enums, `DateOnly` and JSON columns such as `Superpowers[]`.
- **`SqlRepository<T>`** is a generic base repository. `HeroRepository` and `UserRepository` extend it with specific queries such as search, paging and uniqueness checks.

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

## API endpoints

In Development, the API publishes an interactive **Scalar API reference** at `/scalar`, generated from the OpenAPI document at `/openapi/v1.json`. The API's base URL (`/`) redirects to it. Starting the API from Visual Studio or Rider (F5), or with `dotnet watch`, opens it automatically. In the Aspire dashboard, use the **Scalar API Reference** link on the `api` resource. Neither endpoint is exposed outside Development.

### Auth and Users API

| Verb   | Route                 | Auth      | Success | Description                    |
| ------ | --------------------- | --------- | ------- | ------------------------------ |
| POST   | `/api/auth/register`  | Anonymous | 201     | Create a user account          |
| POST   | `/api/auth/login`     | Anonymous | 200     | Return a JWT and user info     |
| GET    | `/api/auth/me`        | User      | 200     | Current user's profile         |
| GET    | `/api/users`          | Admin     | 200     | List users                     |
| DELETE | `/api/users/{id}`     | Admin     | 204     | Soft-delete a user             |

### Heroes API

| Verb   | Route                | Auth  | Success | Errors             | Description                                        |
| ------ | -------------------- | ----- | ------- | ------------------ | -------------------------------------------------- |
| GET    | `/api/heroes`        | User  | 200     | 400, 401           | Paged list (`search`, `page`, `pageSize`, `sortBy`, `sortDir`) |
| GET    | `/api/heroes/{id}`   | User  | 200     | 401, 404           | Hero details                                       |
| POST   | `/api/heroes`        | User  | 201     | 400, 401, 409      | Create a hero (returns a `Location` header)        |
| PUT    | `/api/heroes/{id}`   | User  | 200     | 400, 401, 404, 409 | Update a hero                                      |
| DELETE | `/api/heroes/{id}`   | Admin | 204     | 401, 403, 404      | Soft-delete a hero                                 |

### Other endpoints

| Verb | Route      | Auth      | Description  |
| ---- | ---------- | --------- | ------------ |
| GET  | `/health`  | Anonymous | Readiness: the API and SQL Server are healthy |
| GET  | `/alive`   | Anonymous | Liveness: the API process is responsive       |

Errors are returned as RFC 9457 **ProblemDetails**. Validation errors use `ValidationProblemDetails`.

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
- **SQL Server keeps running** after you stop the AppHost (persistent lifetime), and its data lives in the `hero-registry-sql-data` volume. Restarts are fast, and data survives them. To reset everything, run `docker rm -f` on the `sql-*` container, then `docker volume rm hero-registry-sql-data`.
- **Without Aspire**, you can run the API (`dotnet run --project HeroRegistry/src/HeroRegistry.Api`) and the UI (`npm start` in `hero-registry-ui`) separately. In that case, provide `ConnectionStrings__HeroRegistryDb` yourself.

The database schema and seed data are applied automatically when the API starts (T03).

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

| Role          | User name | Password       |
| ------------- | --------- | -------------- |
| Administrator | `admin`   | `Admin@12345`  |
| Regular user  | `demo`    | `Demo@12345`   |

These accounts are for demo purposes only. Change them, and the JWT key, for any real deployment.

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
