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
8. [Testing](#testing)
9. [Design decisions](#design-decisions)
10. [Generative AI usage](#generative-ai-usage)
11. [Project management](#project-management)

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
| API              | ASP.NET Core 10 Web API (controllers), JWT Bearer authentication, OpenAPI                    |
| Application      | Use-case services, validators and DTOs (plain C#, no MediatR)                                 |
| Domain           | Entities, value objects and domain rules (no framework dependencies)                         |
| Data access      | ADO.NET (`Microsoft.Data.SqlClient`) plus a custom **Reflection-based object mapper**        |
| Database         | SQL Server 2022                                                                              |
| Dev orchestration| .NET Aspire (AppHost and ServiceDefaults: SQL Server, API and Angular with one command)      |
| Delivery         | Docker and Docker Compose                                                                    |
| Testing          | xUnit, NSubstitute, Shouldly, Testcontainers (SQL Server), `WebApplicationFactory`, Vitest   |
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

When the API runs in Development, its OpenAPI document is available at `/openapi/v1.json`.

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
| GET  | `/health`  | Anonymous | Health check |

Errors are returned as RFC 9457 **ProblemDetails**. Validation errors use `ValidationProblemDetails`.

## Getting started

### Prerequisites

| Tool                         | Needed for                    |
| ---------------------------- | ----------------------------- |
| .NET SDK 10                  | Back end and Aspire           |
| Node.js 22 LTS or later and npm | Angular UI                 |
| Docker Desktop               | SQL Server container, Aspire and Compose |
| Aspire CLI (optional)        | `aspire run`                  |

### Run for development (.NET Aspire)

The Aspire AppHost starts SQL Server in a container, the API and the Angular dev server, and wires up their connection strings and URLs.

```bash
# 1. Install the UI dependencies (first run only)
cd hero-registry-ui && npm install && cd ..

# 2. Start everything
dotnet run --project HeroRegistry/src/HeroRegistry.AppHost
#   or: aspire run
```

The **Aspire dashboard** URL is printed in the console. It shows each resource's endpoints, logs, traces and metrics. The database schema and seed data are applied automatically when the API starts.

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

## Testing

The project is developed with **TDD** (red, green, refactor).

```bash
# Back end: all test projects
dotnet test HeroRegistry/HeroRegistry.slnx

# Back end with coverage
dotnet test HeroRegistry/HeroRegistry.slnx --collect:"XPlat Code Coverage"

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
