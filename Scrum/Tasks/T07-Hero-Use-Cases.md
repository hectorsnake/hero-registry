# T07: Hero use cases and validation

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Application · **Estimate:** 6 h · **Depends on:** T04

## Goal
Implement the hero business logic (CRUD use cases and validation), independent of the database and the API.

## Steps
- [ ] DTOs: `CreateHeroRequest`, `UpdateHeroRequest` and `HeroResponse`. `HeroQuery` (with `Normalize()`), `HeroSortBy`, `SortDirection`, `PagedResult<T>` and `DuplicateEntityException` already exist (T06). Reuse them, and map `PagedResult<SuperHero>` to `PagedResult<HeroResponse>`.
- [ ] Ports:
  - `ICurrentUser` (UserId, IsAdmin)
  - `IClock` (UtcNow, Today), for testability
- [ ] Validators (FluentValidation or hand-written `IValidator<T>`): every field rule from the user story's business rules table. Collect **all** field errors and return them together.
  - Reuse the limits in `SuperHeroRules` (T04) instead of repeating numbers. The domain entity rejects only the **first** violation, so these validators are what give the user the complete list of field errors.
  - Map any `DomainException` that still escapes to a `Validation` error on its `Field`.
- [ ] `HeroService` (plain class, no MediatR) behind `IHeroService`:
  - `GetAsync(id)`: returns `NotFound` if missing or deleted.
  - `SearchAsync(query)`: page size is limited to 1 to 100. `HeroQuery.Normalize()` (T06) already does this, and the repository applies it too.
  - `CreateAsync(request)`: validate, check uniqueness (`Conflict`), `SuperHero.Create`, `MarkCreated(currentUser)`, save.
  - `UpdateAsync(id, request)`: validate, `NotFound`, uniqueness check excluding self, `Update` plus `MarkUpdated`.
  - `DeleteAsync(id)`: requires `IsAdmin` (otherwise `Forbidden`), returns `NotFound` if missing, then `SoftDelete`.
- [ ] Return a `Result<T>` (or `ErrorOr`-style) with error types `Validation`, `NotFound`, `Conflict` and `Forbidden`. This avoids exceptions for control flow, and the API maps each error type to an HTTP status.
- [ ] Map entities to DTOs with a hand-written mapper (no AutoMapper needed).

## Tests (TDD, NSubstitute mocks for repositories, ICurrentUser and IClock)
- [ ] Validator tests: one test per rule, including boundaries (PowerLevel 0/1/100/101, zip formats, 0/1/10/11 superpowers, future birth date).
- [ ] Create: success sets audit fields from `ICurrentUser` and `IClock`. A duplicate name or email returns Conflict, and the repository is never called with invalid data.
- [ ] Update: NotFound, Conflict when another hero has the email, and success when the hero keeps its own name.
- [ ] Delete: a non-admin gets Forbidden, a missing hero gets NotFound, and the admin case soft-deletes.
- [ ] Search clamps paging values.

## Done when
- Application coverage is at least 90%, and the project has no reference to Infrastructure or ASP.NET.
