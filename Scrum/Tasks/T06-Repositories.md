# T06: Hero and User repositories

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Application (ports) and Infrastructure · **Estimate:** 5 h · **Depends on:** T05 · **Status:** Done

## Goal
Implement the Application persistence ports on top of the generic `SqlRepository<T>` built in T05.

## Steps
- [x] Ports in **Application** (`Abstractions/Persistence`):
  - `IHeroRepository`: `GetByIdAsync`, `SearchAsync(HeroQuery)` returning `PagedResult<SuperHero>`, `AddAsync`, `UpdateAsync`, `SoftDeleteAsync`, `ExistsByHeroNameAsync(name, excludeId?)`, `ExistsByEmailAsync(email, excludeId?)`
  - `IUserRepository`: `GetByIdAsync`, `GetByUserNameAsync`, `ListAsync`, `AddAsync`, `SoftDeleteAsync`, `ExistsByUserNameAsync(name, excludeId?)`, `ExistsByEmailAsync(email, excludeId?)`
- [x] Supporting Application types (also used by T07 and T10):
  - `HeroQuery` (`Search`, `Page`, `PageSize`, `SortBy`, `SortDirection`) with `Normalize()`: page at least 1, page size 1 to 100, search trimmed and capped at 100 characters, unknown sort values replaced by the defaults.
  - `HeroSortBy` (`HeroName`, `PowerLevel`, `CreatedAt`) and `SortDirection`.
  - `PagedResult<T>` (`Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNextPage`, `HasPreviousPage`).
  - `DuplicateEntityException` (`EntityName`, `Field`).
- [x] Implementations in **Infrastructure** (`Persistence/Repositories`): `HeroRepository : SqlRepository<SuperHero>, IHeroRepository` and `UserRepository : SqlRepository<User>, IUserRepository`. `GetByIdAsync`, `UpdateAsync`, `SoftDeleteAsync` and `ListAsync` come from the base. `AddAsync` wraps `InsertAsync`.
- [x] `SearchAsync`:
  - Parameterized `LIKE` on HeroName, FirstName, LastName and City, plus `EXISTS (SELECT 1 FROM OPENJSON([Superpowers]) WHERE [value] LIKE @Search)`. Matching is case-insensitive (column collation).
  - **LIKE wildcards in the user's text (`%`, `_`, `[`) are escaped** (`ESCAPE '\'`), so they match literally.
  - **Whitelisted** `ORDER BY`: the `HeroSortBy` enum picks one of three known columns, never user text, with `Id` as a tie-breaker for stable paging.
  - `OFFSET`/`FETCH` paging plus `COUNT(*) OVER()` for the total. A page past the end runs a separate `COUNT(*)`, so the total is still correct.
- [x] `GetByUserNameAsync`: an active user, ignoring case and surrounding spaces (for login). A blank name returns null without querying.
- [x] `Exists*`: a shared `ExistsActiveAsync` helper in the base class. It ignores deleted rows and the excluded id, trims the value, and is case-insensitive.
- [x] `SqlException` 2601 and 2627 (unique violations) are translated into `DuplicateEntityException` **in the base `SqlRepository<T>`**, so every write of every repository gets it:
  - The field is found through the index naming convention `UX_{Table}_{Column}`, for example `UX_SuperHeroes_HeroName` gives `HeroName`.
  - The exception carries **no inner exception and no value**: SQL Server's message ("The duplicate key value is (...)") is discarded.
  - The failure is still logged by the data-layer timer, with the SQL error number only (T02.2).
- [x] Every repository operation is timed, traced and logged: inherited from `SqlRepository<T>` (T05).
- [x] Registered in `AddInfrastructure()` as **scoped** services: `IHeroRepository` and `IUserRepository`.

## Tests (TDD)
- [x] Application (`PersistenceContractTests`): `PagedResult` page math, `HeroQuery` defaults and normalization, and `DuplicateEntityException` messages without values.
- [x] Integration (Testcontainers, a freshly migrated and seeded database per test):
  - `HeroRepositoryTests`:
    - CRUD round-trip.
    - Search by name, first name, last name, city and superpower, ignoring case and trimmed. Several matches, no match.
    - **LIKE wildcards are matched literally.**
    - SQL in the search term is data.
    - Deleted heroes are never returned or counted.
    - Paging pages and totals, a page past the end, out-of-range values normalized.
    - Sorting by every whitelisted column and direction. An unknown sort value falls back to the default.
    - `Exists*`: case, spaces, excluded id, deleted heroes.
    - Duplicate name or email on insert and on update gives `DuplicateEntityException` naming the field, without the value.
  - `UserRepositoryTests`:
    - CRUD round-trip with list.
    - `GetByUserNameAsync`: case, spaces, unknown, blank, deleted, injection.
    - `Exists*`.
    - Duplicate user name or email.
  - `SqlRepositoryTests` (T05) updated: unique violations are now `DuplicateEntityException`, and other SQL errors (for example 208, invalid object) still propagate unchanged.
  - DI: the repositories are resolved as the Application ports, one instance per scope.
- [x] Red check: removing the LIKE escaping makes all three wildcard tests fail.

## Done when
- [x] All repository integration tests pass against a real SQL Server container.
- [x] The Release build has 0 warnings. All 428 tests pass: Domain 120, Application 52, Infrastructure 221, ServiceDefaults 20 and Api 15.

## Implementation notes
- **Why translation lives in the base class:** the unique indexes protect every table, and the base class already catches and logs failures. Putting the translation there keeps the concrete repositories free of error handling, and new repositories get it automatically.
- **Naming convention matters:** new unique indexes must follow `UX_{Table}_{Column}` so `DuplicateEntityException.Field` is filled. Otherwise `Field` is null and the message is generic.
- **Scoped lifetime:** the repositories are stateless (a pooled connection per operation), so singleton would also work. Scoped follows the usual convention for request-bound services and costs nothing.
