# T07: Hero use cases and validation

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Application · **Estimate:** 6 h · **Depends on:** T04, T06 · **Status:** Done

## Goal
Implement the hero business logic (CRUD use cases and validation), independent of the database and the API.

## Steps
- [x] DTOs (`Heroes/HeroContracts.cs`):
  - `CreateHeroRequest` and `UpdateHeroRequest` share an abstract `HeroRequest` record. All fields are nullable, so a missing field becomes "X is required." instead of a binding error.
  - `HeroResponse` includes the audit trail (created and updated, by whom and when).
  - Reused from T06: `HeroQuery` (with `Normalize()`), `HeroSortBy`, `SortDirection`, `PagedResult<T>` and `DuplicateEntityException`.
- [x] Ports:
  - `ICurrentUser` (`UserId`, `IsAdmin`) in `Application/Abstractions`.
  - **Clock: .NET's built-in `TimeProvider`** instead of a custom `IClock`. Infrastructure already uses it, and tests use `FakeTimeProvider`. `AddApplication` registers `TimeProvider.System` with `TryAdd`.
- [x] Validator (`HeroRequestValidator`, **hand-written**, so Application gains no package):
  - It checks every rule of the user story and returns **all** field errors at once.
  - Limits come from `SuperHeroRules`. Email, state and zip code are checked by the domain value objects themselves, so no rule or regex is defined twice.
  - Any `DomainException` that still escapes becomes a `Validation` error on its `Field` (`HeroService.ToValidationError`).
- [x] `HeroService` (plain class, no MediatR) behind `IHeroService`:
  - `GetAsync(id)`: `NotFound` if missing or deleted.
  - `SearchAsync(query)`: normalizes the query (null means defaults) and maps the page to `PagedResult<HeroResponse>`.
  - `CreateAsync(request)`:
    1. Requires a signed-in user (`Unauthorized`).
    2. Validates (`Validation`).
    3. `SuperHero.Create` with the current user and the clock.
    4. Checks **both** unique fields with the normalized values (`Conflict` naming every taken field).
    5. Saves. A `DuplicateEntityException` from the database (a race) also becomes `Conflict`.
  - `UpdateAsync(id, request)`: the same steps, in this order: signed-in user, validation (before loading), `NotFound`, `Update` with the update audit, uniqueness **excluding the hero itself**, save. A hero deleted meanwhile gives `NotFound`, and a database duplicate gives `Conflict`.
  - `DeleteAsync(id)`: a signed-in user (`Unauthorized`), `IsAdmin` (otherwise `Forbidden`, without touching the repository), `NotFound`, then `SoftDelete` with the clock. A hero deleted meanwhile gives `NotFound`.
- [x] `Result` and `Result<T>` with an `Error` record (`Type`, `Code`, `Message`, `FieldErrors`).
  - Error types: `Validation`, `Unauthorized` (added for a missing user), `Forbidden`, `NotFound` and `Conflict`.
  - Stable codes: `validation.failed`, `auth.unauthenticated`, `auth.forbidden`, `hero.not_found` and `hero.duplicate`. The API maps each type to an HTTP status (T09, T10).
- [x] Hand-written mapping (`HeroMapping`): entity to `HeroResponse`, and request to `SuperHeroDetails`.
- [x] DI: `IHeroService` is registered as scoped.
- [x] **API, `ICurrentUser` (brought forward from T09):** `HttpCurrentUser` reads the user id from the `sub` (or NameIdentifier) claim and the admin flag from the `Admin` role, and ignores the claims of unauthenticated identities. It's registered in `Program.cs` with `AddHttpContextAccessor`. This was needed now: the API's Development startup validates every service, and `HeroService` requires `ICurrentUser`.

## Tests (TDD, NSubstitute mocks for the repository and ICurrentUser, FakeTimeProvider)
- [x] `HeroRequestValidatorTests`:
  - One test per rule, with boundaries: text lengths at min-1, min, max and max+1, and measured after trimming; PowerLevel 0, 1, 100 and 101; 0, 1, 10 and 11 superpowers, counted distinct and ignoring case; names of 1 and 51 characters, blank and null; birth date today, tomorrow, yesterday, and exactly 150 years ago or one day more; email, state and zip formats, plus the normalized values that are accepted.
  - All errors are reported together, and the messages are readable.
- [x] `HeroServiceTests`:
  - Get: found, NotFound.
  - Search: the query is normalized and the page mapped; a null query uses the defaults.
  - Create: audit from `ICurrentUser` and the clock; normalization; validation with no repository calls; duplicate name, duplicate email, both duplicates; uniqueness checked with the normalized email; a database duplicate gives Conflict; Unauthorized.
  - Update: the update audit, with the creation audit kept; the hero keeps its own name (exclude id); Conflict on another hero's email; NotFound; deleted meanwhile; validated before loading; database duplicate; Unauthorized.
  - Delete: an admin soft-deletes; a regular user gets Forbidden with no repository calls; NotFound; deleted meanwhile; Unauthorized.
  - The `DomainException` safety net.
- [x] `ResultTests`, `DependencyInjectionTests` (resolved with `ValidateOnBuild`), and `HttpCurrentUserTests` in Api.Tests.

## Done when
- [x] Application coverage is at least 90%: **98.0% line coverage**. Every file is at 100% except `HeroService` (94.6%), whose uncovered lines are the domain safety-net branches that the validator makes unreachable.
- [x] The project has no reference to Infrastructure or ASP.NET (the architecture tests pass).
- [x] The Release build has 0 warnings. All 503 tests pass: Domain 120, Application 117, Infrastructure 221, ServiceDefaults 20 and Api 25.

## Implementation notes
- **Field names in errors** are the property names (`HeroName`, `PowerLevel`). The API can camel-case them in ProblemDetails (T11).
- **Validation runs before any database call**, and uniqueness after the domain has normalized the values. So `" Black.Widow@HeroRegistry.com "` is checked as `black.widow@heroregistry.com`.
