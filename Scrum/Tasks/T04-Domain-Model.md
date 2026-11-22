# T04: Domain model, entities and invariants

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Domain · **Estimate:** 3 h · **Depends on:** T01 · **Status:** Done

## Goal
Model `SuperHero` and `User` as rich, framework-free entities that protect their own invariants. These are **not** database-mapping classes. They have the same shape as the tables, so the Reflection mapper (T05) can load and save them, but they contain no SQL, attributes or table names. That knowledge lives in Infrastructure.

## Steps
- [x] `AuditableEntity` base class (`Domain/Common`):
  - Properties: `Id`, `IsDeleted`, `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`
  - Methods: `SoftDelete(userId, utcNow)` (public); `MarkCreated`, `MarkUpdated` and `EnsureValidUpdate` (only for entities in this assembly)
  - `SoftDelete` on an already-deleted entity throws `DomainException`.
  - Audit times must be UTC and can't precede creation. User ids must be positive.
- [x] `SuperHero` entity (`Domain/Heroes`):
  - Properties: HeroName, FirstName, LastName, BirthDate (`DateOnly`), Email, `IReadOnlyList<string> Superpowers`, PowerLevel, Address1, Address2, City, State, ZipCode
  - `SuperHero.Create(SuperHeroDetails, createdBy, utcNow)` and `Update(SuperHeroDetails, updatedBy, utcNow)`. `SuperHeroDetails` is a record holding the editable fields.
  - Normalizes input: trims strings, upper-cases `State`, lower-cases `Email`, stores a blank `Address2` as null, and trims and de-duplicates `Superpowers` case-insensitively (keeping the first). The resulting list is read-only.
  - Enforces:
    - PowerLevel 1 to 100.
    - BirthDate in the past and at most 150 years ago. "Today" comes from `utcNow`, never `DateTime.Now`.
    - 1 to 10 distinct superpowers of 2 to 50 characters each.
    - The text length limits.
    - Valid email, state and zip code.
  - `Update` checks everything **before** changing anything, so an invalid update leaves the hero untouched. It never changes the creation audit, and it rejects deleted heroes.
- [x] `User` entity (`Domain/Users`):
  - Properties: UserName, Email, `Password` (hash), IsAdmin
  - `User.Register(userName, email, passwordHash, utcNow, createdBy = null)` always sets `IsAdmin = false`.
  - UserName: 3 to 50 characters, `^[a-zA-Z0-9._-]+$`. Email is normalized. The password hash is required and at most 200 characters.
- [x] `DomainException(field, message)`. `Field` names the offending property, for field-level API errors.
- [x] Value objects (`Domain/ValueObjects`): `EmailAddress`, `UsStateCode` (50 states, DC and 5 territories) and `ZipCode`.
- [x] Rule constants in `SuperHeroRules` and `UserRules`, so the Application validators (T07, T08) reuse the same limits.
- [x] Entities have **no public setters and no public constructors**. Each has a **private parameterless constructor** and **private setters** (`private protected` in the base class) that only the Reflection mapper (T05) uses to load rows. All changes go through `Create`, `Update`, `Register` and `SoftDelete`.

## Tests (TDD, written first)
120 Domain tests, all passing:
- [x] `SuperHero.Create`:
  - A valid hero is created.
  - Each rule throws `DomainException` with the right `Field`.
  - Boundaries: PowerLevel 0, 1, 100 and 101; text lengths at min-1, min, max and max+1; 0, 1, 10 and 11 superpowers; superpower names of 1, 2, 50 and 51 characters; birth date today and yesterday, and exactly 150 years ago or one day more.
- [x] Normalization: `" ny "` becomes `"NY"`; `[" Flight ", "flight", "FLIGHT"]` becomes `["Flight"]`; emails are lower-cased; a blank Address2 becomes null; the stored list can't be changed from outside.
- [x] `Update`: replaces the details and sets the update audit, keeps the creation audit, leaves the hero unchanged when invalid, checks the birth date against the update date, and rejects deleted heroes.
- [x] Audit: creation and update fields, soft delete, soft-deleting twice throws, invalid user ids, non-UTC times, and times before creation.
- [x] `User.Register` never creates an admin; also user name rules, email normalization, the required hash, the creator, and soft delete.
- [x] Value objects: valid and invalid inputs, normalization, and custom field names.
- [x] `EncapsulationTests`: entities have no public setters or constructors, and can be hydrated through reflection exactly as the T05 mapper will.
- [x] T03.1 follow-up: the three seeded heroes from `0003_SeedData.sql` pass `SuperHero.Create` unchanged.

## Done when
- [x] All Domain tests pass, and the Domain project has no package references (the architecture tests in Application.Tests pass).
- [x] The Release build of the whole solution has 0 warnings. All test projects pass: Domain 120, Application 31, Infrastructure 100, ServiceDefaults 20 and Api 15, for 286 in total.

## Implementation notes
- **Regexes:** `new Regex(..., RegexOptions.CultureInvariant, timeout)` instead of `[GeneratedRegex]`. The source generator puts helper types in a `System.Text.RegularExpressions.Generated` namespace, which would break the "all Domain types live in `HeroRegistry.Domain`" convention test. The 100 ms timeout guards against slow regex matching (ReDoS).
- **Why `private protected` in the base class:** a `private` setter declared in a base class is invisible to reflection through the derived type, so the mapper could not set `Id` or the audit fields. `private protected` stays inaccessible to other assemblies, but reflection can see it.
- **What T05 must do:** create entities with `Activator.CreateInstance(type, nonPublic: true)` and set properties with `PropertyInfo.SetValue`. The private setters are reachable through `GetProperty(...).SetMethod`. `Superpowers` is assigned a `string[]` from the JSON column.
