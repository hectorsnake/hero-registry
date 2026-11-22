# US-001: SuperHero Registry

| Field          | Value                                  |
| -------------- | -------------------------------------- |
| **ID**         | US-001                                 |
| **Epic**       | SuperHero Registry MVP                 |
| **Priority**   | High                                   |
| **Estimate**   | 21 story points                        |
| **Status**     | To Do                                  |

---

## User story

> **As a** registered member of the Hero Registration Office,
> **I want to** sign up, log in and then create, view, search, update and delete superhero records,
> **so that** the agency keeps an accurate, secure and auditable registry of every active superhero, their powers and how to contact them.

### Personas

| Persona           | Description                                                                                        |
| ----------------- | -------------------------------------------------------------------------------------------------- |
| **Visitor**       | Not logged in. Can only register an account or log in.                                             |
| **Registry Agent**| Authenticated user. Can list, search, view, create and update heroes.                              |
| **Registry Admin**| Authenticated user with `IsAdmin = true`. Can do everything an agent can, plus delete heroes and manage users. |

---

## Acceptance criteria

### AC1: User registration
- **Given** I am a visitor
- **When** I submit a unique user name, a valid unique email and a password that meets the policy (at least 8 characters, with uppercase, lowercase, a digit and a symbol)
- **Then** my account is created (`201 Created`), my password is stored only as a hash, and I can log in.
- **And** if the user name or email already exists, I get `409 Conflict`. If the data is invalid, I get `400 Bad Request` with field-level errors.

### AC2: Login
- **Given** I have an active account
- **When** I submit valid credentials
- **Then** I receive a JWT (`200 OK`) that includes my user id, user name and role (`Admin` or `User`).
- **And** invalid credentials, or a deleted account, return `401 Unauthorized` with a generic message that does not reveal which part was wrong.

### AC3: Protected endpoints
- **Given** I am not authenticated
- **When** I call any `/api/heroes` or `/api/users` endpoint
- **Then** I get `401 Unauthorized`.
- **And** `/api/auth/register`, `/api/auth/login` and `/health` remain accessible anonymously.

### AC4: Create a hero
- **Given** I am logged in
- **When** I submit a valid hero
- **Then** it is saved with `CreatedBy` set to my user id and `CreatedAt` set to the current UTC time, and I get `201 Created` with a `Location` header.
- **And** a duplicate `HeroName` or `Email` among active heroes returns `409 Conflict`. Invalid data returns `400` with field-level errors.

### AC5: List and search heroes
- **Given** I am logged in
- **When** I open the hero list
- **Then** I see a paged list of active heroes only (default 10 per page, maximum 100).
- **And** I can search by hero name, first name, last name, city or superpower, and sort by HeroName, PowerLevel or CreatedAt.

### AC6: View a hero
- **Given** I am logged in
- **When** I request a hero by id
- **Then** I get its details (`200`), or `404` if it does not exist or was deleted.

### AC7: Update a hero
- **Given** I am logged in
- **When** I submit valid changes for an existing hero
- **Then** the changes are saved with `UpdatedBy` set to my id and `UpdatedAt` set to the current UTC time, and I get `200 OK` with the updated hero.
- **And** `CreatedBy` and `CreatedAt` never change. A missing hero returns `404`. A duplicate name or email returns `409`.

### AC8: Delete a hero
- **Given** I am logged in as an **admin**
- **When** I delete a hero
- **Then** it is soft-deleted (`IsDeleted = 1`, and `UpdatedBy` and `UpdatedAt` are set), I get `204 No Content`, and it no longer appears in lists or lookups.
- **And** a non-admin user gets `403 Forbidden`.

### AC9: User interface
- The Angular UI provides login and register pages, a hero list with search, sort and paging, and a hero detail page plus a create/edit form with client-side validation that mirrors the server rules.
- The delete action is visible only to admins and asks for confirmation before deleting.
- The UI is responsive (usable from 360 px wide up to desktop) and shows no errors or warnings in the browser console.
- Server validation errors (`400`, `409`) are shown next to the relevant fields. A `401` response first triggers a silent token refresh. Only when the refresh fails does it redirect to the login page (AC12).

### AC10: Demo readiness
- On first start, the database is created, migrated and seeded with an administrator (`Admin` / `Admin`, `admin@heroregistry.com`) and three heroes: Captain America, Spider-Man and Superman.
- The whole system starts with **one command** for development (.NET Aspire) and **one command** for delivery (`docker compose up`).

---

### AC11: Update a user account
- **Given** I am signed in
- **When** I update **my own** account (`PUT /api/users/{myId}`) with a valid user name and email
- **Then** the changes are saved with `UpdatedBy` set to my id and `UpdatedAt` set to the current UTC time, and I get `200 OK` with the updated account.
- **And** as an **administrator**, I can update **any** user's account the same way.
- **And** as a **regular user**, updating **someone else's** account returns `403 Forbidden` and nothing changes. This is checked before looking the account up, so the response never reveals whether that user id exists.
- **And** a missing or deleted account returns `404 Not Found` (for my own id, or any id as an administrator). Invalid data returns `400 Bad Request` with field-level errors. A user name or email already used by **another** active account returns `409 Conflict`; keeping my own values is allowed.
- **And** an update never changes the password, the administrator flag (`IsAdmin`) or the creation audit (`CreatedBy`, `CreatedAt`). An anonymous request returns `401 Unauthorized`.

### AC12: Secure, uninterrupted session
- **Given** I am signed in
- **When** my 15-minute access token expires, or I reload the page (F5)
- **Then** the UI silently gets a new access token through `POST /api/auth/refresh`, using the rotated refresh token in an **HttpOnly, Secure, SameSite=Strict cookie**, and I keep working without seeing the login page.
- **And** the access token is kept **in memory only**. No token is ever stored in `localStorage` or `sessionStorage`.
- **And** when the refresh token is expired, revoked or reused (a possible theft, which revokes the whole token family), I am redirected to the login page and returned to my previous page after signing in.
- **And** logging out (`POST /api/auth/logout`) revokes the refresh token, deletes the cookie, and signs out my other open tabs. Deleting a user revokes all of that user's refresh tokens.

## Business rules

### SuperHero

| Field       | Rule                                                                                |
| ----------- | ----------------------------------------------------------------------------------- |
| HeroName    | Required, 2 to 100 characters, unique among non-deleted heroes (case-insensitive)   |
| FirstName   | Required, 1 to 100 characters                                                       |
| LastName    | Required, 1 to 100 characters                                                       |
| BirthDate   | Required, in the past, and no more than 150 years ago                               |
| Email       | Required, valid format, at most 256 characters, unique among non-deleted heroes     |
| Superpowers | 1 to 10 items. Each is 2 to 50 characters, trimmed, with no duplicates (case-insensitive) |
| PowerLevel  | Integer from 1 to 100                                                               |
| Address1    | Required, at most 200 characters                                                    |
| Address2    | Optional, at most 200 characters                                                    |
| City        | Required, at most 100 characters                                                    |
| State       | Required, a valid 2-letter US state code (stored in uppercase)                      |
| ZipCode     | Required, `^\d{5}(-\d{4})?$`                                                        |
| Audit       | `CreatedBy`/`CreatedAt` are set on insert. `UpdatedBy`/`UpdatedAt` are set on update and delete. The client can never set them. |
| IsDeleted   | Soft delete only. Deleted heroes are excluded from every query.                     |

### User

| Field     | Rule                                                                            |
| --------- | ------------------------------------------------------------------------------- |
| UserName  | Required, 3 to 50 characters, `^[a-zA-Z0-9._-]+$`, unique among active users (case-insensitive) |
| Email     | Required, valid format, unique among active users                               |
| Password  | Policy: at least 8 characters, with uppercase, lowercase, a digit and a symbol. Stored as a BCrypt hash |
| IsAdmin   | Defaults to `false`. Self-registration can never create an admin. Not editable (see Out of scope) |
| IsDeleted | Deleted users cannot log in                                                     |
| Who can update an account | The account owner, or an administrator (any account). Only UserName and Email are editable (AC11) |

---

## Technical constraints

- **Back end:** .NET 10, ASP.NET Core Web API (controllers), **Clean Architecture** (Domain, Application, Infrastructure and Api projects).
- **Database:** SQL Server, with the `Users` and `SuperHeroes` tables, plus `RefreshTokens` for sessions (T09.1).
- **Data access:** ADO.NET with a custom **Reflection-based mapper** that persists objects to table rows. **Entity Framework, Dapper and MediatR are forbidden.**
- **Business logic:** lives in the Application and Domain layers, independent of the data layer and the API.
- **Front end:** Angular (`hero-registry-ui`).
- **Development:** .NET Aspire orchestrates SQL Server, the API and the UI.
- **Delivery:** Docker Compose (SQL Server, API and UI served by nginx).
- **Methodology:** TDD. Every component (data access, business logic and API endpoints) has unit and/or integration tests.

## Non-functional requirements

- **Security:** JWT Bearer authentication with short-lived access tokens kept in memory in the UI, and rotated refresh tokens in HttpOnly cookies (only their SHA-256 hash is stored), password hashing, parameterized SQL only, CORS restricted to the UI origin, and secrets supplied through configuration or environment variables (never hard-coded in production settings).
- **Observability:** structured logging, plus health checks and OpenTelemetry through Aspire ServiceDefaults.
- **Errors:** consistent RFC 9457 ProblemDetails responses. Unhandled exceptions never leak stack traces.
- **Quality:** no compiler warnings (`TreatWarningsAsErrors` in src) and nullable reference types enabled.

## Definition of Done

- [ ] Code follows Clean Architecture dependency rules (verified by tests or a review).
- [ ] Tests were written first (TDD) and pass. Back-end line coverage is at least 80% for Domain and Application.
- [ ] All acceptance criteria are verified manually through the UI and through `HeroRegistry.Api.http`.
- [ ] The CI pipeline passes (build and test for the back end and front end).
- [ ] `docker compose up` works on a clean machine, using seeded demo data.
- [ ] The README is updated with setup instructions, the architecture and demo credentials.
- [ ] There are no warnings in the browser console.

## Out of scope

- Password reset, password change and email confirmation. (Refresh tokens moved into scope: AC12, T09.1.)
- Editing a user's admin flag (admins are seeded). Account updates (AC11) cover only the user name and email.
- Restoring deleted records (undelete).
- Hero images or file uploads.

---

## Task breakdown

| ID  | Task                                                                                         | Layer           | Estimate | Depends on |
| --- | -------------------------------------------------------------------------------------------- | --------------- | -------- | ---------- |
| T01 | [Solution restructure and Clean Architecture skeleton](Tasks/T01-Solution-Skeleton.md)        | All             | 3 h      | none       |
| T02 | [.NET Aspire orchestration (AppHost and ServiceDefaults)](Tasks/T02-Aspire-Orchestration.md)  | DevOps          | 3 h      | T01        |
| T02.1 | [OpenAPI document and Scalar API reference](Tasks/T02.1-OpenAPI-Scalar.md)                  | API             | 2 h      | T02        |
| T02.2 | [Logging and telemetry with Azure Application Insights](Tasks/T02.2-Application-Insights-Logging.md) | Cross-cutting | 4 h | T02 (DB part with T05, T06) |
| T03 | [Database schema and migration runner](Tasks/T03-Database-Schema-Migrations.md)               | Infrastructure  | 4 h      | T01, T02   |
| T03.1 | [Seed data (administrator and demo heroes)](Tasks/T03.1-Seed-Data.md)                       | Infrastructure  | 2 h      | T03        |
| T04 | [Domain model: entities and invariants](Tasks/T04-Domain-Model.md)                            | Domain          | 3 h      | T01        |
| T05 | [Reflection-based data mapper](Tasks/T05-Reflection-Data-Mapper.md)                           | Infrastructure  | 8 h      | T03, T04   |
| T06 | [Hero and User repositories](Tasks/T06-Repositories.md)                                       | Infrastructure  | 5 h      | T05        |
| T07 | [Hero use cases and validation](Tasks/T07-Hero-Use-Cases.md)                                  | Application     | 6 h      | T04        |
| T08 | [User registration and login use cases (hashing and JWT)](Tasks/T08-Auth-Use-Cases.md)        | Application and Infrastructure | 5 h | T04 |
| T09 | [Auth and Users API, including account update (AC11)](Tasks/T09-Auth-Users-API.md)        | Application and API | 6 h | T06, T08   |
| T09.1 | [Refresh tokens (HttpOnly cookie, rotation and logout)](Tasks/T09.1-Refresh-Tokens.md)   | Domain, Application, Infrastructure and API | 5 h | T06, T08, T09 |
| T10 | [Heroes API](Tasks/T10-Heroes-API.md)                                                         | API             | 5 h      | T06, T07, T09 |
| T11 | [API cross-cutting concerns](Tasks/T11-API-Cross-Cutting.md)                                  | API             | 3 h      | T01        |
| T12 | [Angular core: auth service, interceptor, guards and layout](Tasks/T12-Angular-Core.md)       | UI              | 7 h      | T09, T09.1 |
| T13 | [Angular login and register pages](Tasks/T13-Angular-Auth-Pages.md)                           | UI              | 4 h      | T12        |
| T14 | [Angular hero management (list, detail, form, delete)](Tasks/T14-Angular-Hero-Management.md)  | UI              | 10 h     | T10, T12   |
| T15 | [Docker Compose delivery](Tasks/T15-Docker-Compose.md)                                        | DevOps          | 4 h      | T10, T14   |
| T16 | [CI pipeline (GitHub Actions)](Tasks/T16-CI-Pipeline.md)                                      | DevOps          | 2 h      | T01        |
| T17 | [Documentation, GenAI write-up and presentation](Tasks/T17-Documentation-Presentation.md)     | Docs            | 4 h      | all        |

**Total estimate:** about 95 hours.

### Suggested order

| Phase | Tasks | Focus |
| ----- | ----- | ----- |
| 1. Foundation   | T01, T02, T02.1, T02.2, T16 | Solution skeleton, Aspire, OpenAPI and Scalar, Application Insights, CI |
| 2. Core         | T03, T03.1, T04, T07, T08 | Schema, seed data, domain and business rules (TDD) |
| 3. Persistence  | T05, T06            | Reflection mapper and repositories |
| 4. API          | T11, T09, T09.1, T10 | Cross-cutting concerns, Auth/Users API, refresh tokens, Heroes API |
| 5. UI           | T12, T13, T14       | Angular core, auth pages, hero management |
| 6. Delivery     | T15, T17            | Docker Compose, documentation and presentation |
