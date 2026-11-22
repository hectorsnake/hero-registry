# T04: Domain model, entities and invariants

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Domain · **Estimate:** 3 h · **Depends on:** T01

## Goal
Model `SuperHero` and `User` as rich, framework-free entities that protect their own invariants.

## Steps
- [ ] `AuditableEntity` base class:
  - Properties: `Id`, `IsDeleted`, `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`
  - Methods: `MarkCreated(userId, utcNow)`, `MarkUpdated(userId, utcNow)`, `SoftDelete(userId, utcNow)`
  - `SoftDelete` on an already-deleted entity throws `DomainException`.
- [ ] `SuperHero` entity:
  - Properties: HeroName, FirstName, LastName, BirthDate (`DateOnly`), Email, `IReadOnlyList<string> Superpowers`, PowerLevel, Address1, Address2, City, State, ZipCode
  - Factory `SuperHero.Create(...)` and method `Update(...)`
  - Normalizes input: trims strings, uppercases `State`, de-duplicates `Superpowers` case-insensitively.
  - Enforces: PowerLevel 1 to 100, BirthDate in the past (receives `today` as a parameter, no `DateTime.Now`), and 1 to 10 superpowers.
- [ ] `User` entity:
  - Properties: UserName, Email, `Password` (hash), IsAdmin
  - Factory `User.Register(userName, email, passwordHash)` sets `IsAdmin = false`.
- [ ] `DomainException` and value objects where useful (`EmailAddress`, `UsStateCode`, `ZipCode`).
- [ ] Keep public parameterless or private constructors plus settable properties so that the Reflection mapper (T05) can hydrate entities.

## Tests (TDD, write first)
- [ ] `SuperHero.Create`: valid data succeeds. Each invalid rule throws `DomainException` (boundary values for PowerLevel 0, 1, 100 and 101).
- [ ] Normalization: `" ny "` becomes `"NY"`, and the superpowers `["Flight", "flight"]` become `["Flight"]`.
- [ ] Audit methods set the correct fields. `SoftDelete` twice throws.
- [ ] `User.Register` never creates an admin.

## Done when
- 100% of Domain tests pass, and the Domain project has no package references.
