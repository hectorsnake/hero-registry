# T10: Heroes API

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** API · **Estimate:** 5 h · **Depends on:** T06, T07, T09

## Goal
Expose the hero CRUD endpoints with correct HTTP verbs, parameters, status codes and authorization.

## Steps
- [ ] `HeroesController` (`/api/heroes`, `[Authorize]`):
  - `GET` with `[FromQuery] HeroQuery`: returns 200 + `PagedResult<HeroResponse>`.
  - `GET {id:int}`: returns 200 or 404.
  - `POST` with `[FromBody] CreateHeroRequest`: returns 201 with `CreatedAtAction` (Location header), 400 or 409.
  - `PUT {id:int}` with `[FromBody] UpdateHeroRequest`: returns 200, 400, 404 or 409.
  - `DELETE {id:int}` with `[Authorize(Policy = "AdminOnly")]`: returns 204, 403 or 404.
- [ ] Controllers stay thin: no business logic, only delegation to `IHeroService` and result mapping. `IHeroService`, `CreateHeroRequest`, `UpdateHeroRequest`, `HeroResponse`, `HeroQuery` and `Result` already exist (T07). The service also enforces authentication and the admin-only delete, so the `[Authorize]` attributes are defense in depth.
  - Ready from T09:
    - Derive from `ApiControllerBase` (`OkOrError`, `NoContentOrError`, `ErrorResult`: problem details with `code` and camelCase `errors`).
    - Use `AuthenticationExtensions.AdminOnlyPolicy` for `DELETE`. The fallback policy already requires a signed-in user.
  - For `POST`, return `CreatedAtAction(nameof(Get), new { id }, hero)` so the Location header points to `GET /api/heroes/{id}`.
- [ ] Bind `BirthDate` as `DateOnly` in `yyyy-MM-dd` format. `Superpowers` is a JSON array.
- [ ] Use `CancellationToken` on every action.
- [ ] Add samples to `HeroRegistry.Api.http` (log in, then use the token variable).

## Tests (TDD, `WebApplicationFactory`)
Reuse the T09 test infrastructure: `DatabaseApiFactory` (a fresh, migrated and seeded database per class), and `ApiClientExtensions` (`SignedInClientAsync(AdminUserName, AdminPassword)`, `NewSignedInUserAsync()` for a regular user, `ReadJsonAsync()` for problem details).

- [ ] Every endpoint returns 401 without a token.
- [ ] Full CRUD flow as a regular user (registered by the test): create (201 and Location), get, update, then list contains the hero.
- [ ] `DELETE` as a regular user returns 403. As `Admin` it returns 204, and a later `GET` returns 404.
- [ ] The seeded heroes (Captain America, Spider-Man, Superman) are listed and searchable, for example by searching for "Flight".
- [ ] Validation returns 400 with field errors. A duplicate hero name returns 409.
- [ ] Paging and search query strings are bound correctly.
- [ ] The created hero has `createdBy` equal to the id of the caller.

## Done when
- All AC4 to AC8 scenarios from US-001 are covered by passing API tests.
