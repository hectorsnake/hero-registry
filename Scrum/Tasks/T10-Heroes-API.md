# T10: Heroes API

**User story:** [US-001](../US-001-SuperHero-Registry.md) (AC3 to AC8) · **Layer:** API · **Estimate:** 5 h · **Depends on:** T06, T07, T09 · **Status:** Done

## Goal
Expose the hero CRUD endpoints with correct HTTP verbs, parameters, status codes and authorization.

## Steps
- [x] `HeroesController` (`/api/heroes`, `[Authorize]`, deriving from `ApiControllerBase`):
  - `GET` with `[FromQuery] HeroQuery`: 200 + `PagedResult<HeroResponse>`, 400 (for example an unknown `sortBy`) or 401.
  - `GET {id:int}`: 200, 401 or 404.
  - `POST` with `CreateHeroRequest`: **201 via `CreatedAtAction(nameof(Get))`**, so the Location header is `/api/heroes/{id}`. Also 400, 401 or 409.
  - `PUT {id:int}` with `UpdateHeroRequest`: 200, 400, 401, 404 or 409.
  - `DELETE {id:int}` with `[Authorize(Policy = AdminOnly)]`: 204, 401, 403 or 404.
- [x] Thin controller: each action is one call to `IHeroService` plus the shared result mapping. The service also enforces authentication and the admin-only delete, so the attributes are defense in depth.
- [x] Binding:
  - `HeroQuery` binds from the query string by name: `search`, `page`, `pageSize`, `sortBy`, `sortDirection` (enum values by name, for example `PowerLevel` or `Descending`). Missing values take the record's defaults.
  - `BirthDate` is `DateOnly` in `yyyy-MM-dd`, and `Superpowers` is a JSON array. A malformed body (for example `"1990-13-45"`) gets the automatic 400 from `[ApiController]`.
- [x] `CancellationToken` on every action.
- [x] `[ProducesResponseType]` for every status code (OpenAPI and Scalar).
- [x] `HeroRegistry.Api.http`: search, get, create (the id is captured), update and delete, using the token from the login sample.
- [x] README: the list parameters and response shape, and the date and superpower formats. The README's `sortDir` became `sortDirection`, the actual name.

## Tests (TDD, `WebApplicationFactory` with Testcontainers SQL Server)
Written first: 17 of the 22 failed before the controller existed. The 5 "401 without a token" tests already passed thanks to the T09 fallback policy.

`HeroesEndpointsTests` (`DatabaseApiFactory`: the API migrates and seeds its own database):
- [x] **AC3:** all five endpoints return 401 without a token.
- [x] **AC5:**
  - The seeded heroes are searchable by superpower ("Flight" finds Superman).
  - JSON shape: `birthDate` is `"1977-06-18"`, `superpowers` is an array, camelCase properties.
  - Defaults: page 1, size 10, sorted by name.
  - **Search, paging and sorting bound from the query string**: 3 tagged heroes, sorted by power level descending, pages of 2 give `[90, 50]` then `[10]`, with a total of 3 and 2 pages.
  - Out-of-range paging is normalized (page 0 becomes 1, size 5000 becomes 100). An unknown `sortBy` returns 400.
- [x] **AC4, AC6, AC7 (full CRUD as a regular user):**
  - Create returns 201 with a Location pointing to `/api/heroes/{id}`, and **`createdBy` is the caller's id**.
  - Get through the Location.
  - Update returns 200, with `updatedBy` set to the caller and `createdBy` unchanged.
  - Search finds it.
  - Input is normalized (trimmed name, `"ny"` becomes `"NY"`, lower-cased email). A hero can keep its own name.
- [x] **Validation and conflicts:**
  - Invalid data returns 400 with every field in `errors` (camelCase) and code `validation.failed`. An empty body reports all 11 required fields. A malformed date returns 400.
  - A duplicate name (`"SUPERMAN"`) returns 409 with code `hero.duplicate` and `errors.heroName`. Renaming to an existing name returns 409.
  - An unknown id returns 404 (`hero.not_found`) for GET and PUT.
- [x] **AC8:**
  - A regular user's DELETE returns 403, and the hero still exists.
  - Admin's DELETE returns 204. A later GET returns 404, a search no longer counts it, and a second DELETE returns 404.

## Done when
- [x] Every AC4 to AC8 scenario of US-001 is covered by passing API tests (AC3 too).
- [x] All tests pass: Domain 126, Application 187, Infrastructure 252, ServiceDefaults 20 and Api 77, for **662** in total. The Release build has 0 warnings.

## Implementation notes
- **An unknown `sortBy` is a 400 at the API** (model binding rejects an undefined enum name), while `HeroQuery.Normalize()` still protects the use case from out-of-range numeric values. The automatic model-binding 400 has a slightly different shape (no `code`) than the use-case 400s; T11 can unify them.
