# T06: Hero and User repositories

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Infrastructure · **Estimate:** 5 h · **Depends on:** T05

## Goal
Implement the Application persistence ports on top of the generic `SqlRepository<T>`.

## Steps
- [ ] Define the ports in **Application** (`Abstractions/Persistence`):
  - `IHeroRepository`: `GetByIdAsync`, `SearchAsync(HeroQuery)` returns `PagedResult<SuperHero>`, `AddAsync`, `UpdateAsync`, `SoftDeleteAsync`, `ExistsByHeroNameAsync(name, excludeId?)`, `ExistsByEmailAsync(email, excludeId?)`
  - `IUserRepository`: `GetByIdAsync`, `GetByUserNameAsync`, `ListAsync`, `AddAsync`, `SoftDeleteAsync`, `ExistsByUserNameAsync`, `ExistsByEmailAsync`
- [ ] Implement them in **Infrastructure**: `HeroRepository : SqlRepository<SuperHero>, IHeroRepository`, and the same pattern for `UserRepository`.
  - Pass `new SuperHeroMap()` or `new UserMap()` to the base constructor. `GetByIdAsync`, `InsertAsync` (`AddAsync`), `UpdateAsync` and `SoftDeleteAsync` already exist and are instrumented (T05).
  - Build custom queries from `SelectFromTable` and `SelectList`, with `Parameter(column, value, "@Name")` for typed parameters. Paging can read `COUNT(*) OVER()` through `QueryAsync`'s `readRow` callback, as `SqlRepositoryTests.FirstPageAsync` shows.
- [ ] `SearchAsync`:
  - Parameterized `LIKE` on HeroName, FirstName, LastName and City.
  - `EXISTS (SELECT 1 FROM OPENJSON(Superpowers) WHERE value LIKE @search)`.
  - **Whitelisted** `ORDER BY` column (HeroName, PowerLevel or CreatedAt) and direction.
  - `OFFSET`/`FETCH` paging plus `COUNT(*) OVER()` for the total.
- [ ] Translate `SqlException` 2601/2627 (unique index violation) into a `DuplicateEntityException` from Application. This is a safety net for race conditions. The new exception must **not** carry SQL Server's message, which contains the duplicate value (personal data). See [T02.2](T02.2-Application-Insights-Logging.md).
- [ ] Wrap every repository operation in `DataAccessInstrumentation.Begin(...)` (T02.2) so it is timed, traced and logged.
- [ ] Register the repositories in `AddInfrastructure()`.

## Tests (TDD, integration with Testcontainers)
- [ ] CRUD round-trip for each repository.
- [ ] Search matches on name, city and superpower. Paging returns the correct `TotalCount`. Deleted heroes are never returned.
- [ ] Sorting by a non-whitelisted column is rejected or falls back to the default.
- [ ] The `Exists*` methods ignore deleted rows and the excluded id.
- [ ] A unique violation is mapped to `DuplicateEntityException`.

## Done when
- All repository integration tests pass against a real SQL Server container.
