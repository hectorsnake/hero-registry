# T05: Reflection-based data mapper

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Infrastructure · **Estimate:** 8 h · **Depends on:** T03, T04 · **Status:** Done

## Goal
Build a small, generic, **Reflection-based** persistence engine on top of ADO.NET (`Microsoft.Data.SqlClient`) that maps objects to table rows and back. Entity Framework, Dapper and MediatR are not allowed.

## Design (as built)
```
Infrastructure/Persistence/
├── Mapping/
│   ├── EntityMap<T>, ColumnBuilder   # fluent mapping, configured in Infrastructure
│   ├── AuditableEntityMap<T>         # key, insert-only creation audit, soft delete
│   ├── EntityMetadata, ColumnMetadata# Reflection, cached per map type
│   ├── ValueConverter                # property value to and from database value
│   └── DataRecordMapper<T>           # data record to entity
├── Maps/
│   ├── SuperHeroMap                  # SuperHero to dbo.SuperHeroes
│   └── UserMap                       # User to dbo.Users
├── Sql/
│   ├── SqlStatement, SqlParameterValue
│   ├── SqlStatementBuilder           # parameterized SELECT/INSERT/UPDATE/soft DELETE
│   └── IDbConnectionFactory, SqlConnectionFactory
└── SqlRepository<T>                  # generic CRUD base, instrumented (T02.2)
```

**Mapping choice: fluent `EntityMap<T>`, not attributes.** The domain entities (T04) carry no persistence attributes. `SuperHeroMap` and `UserMap` map them **directly**, so there is no duplicate set of persistence classes. The planned attributes became fluent calls:

| Attribute in the plan | Fluent equivalent |
| --- | --- |
| `[Table]` | `ToTable` |
| `[Key]` | `HasKey` |
| `[Column]` | `Property(...).HasColumnName` / `HasMaxLength` / `IsAnsi` / `IsFixedLength` |
| `[NotMapped]` | `Ignore` |
| `[JsonColumn]` | `StoredAsJson` |
| `[SoftDelete]` | `HasSoftDelete` |
| (new) | `IsInsertOnly` |

## Steps
- [x] `EntityMetadata` (built from the map, cached in a `ConcurrentDictionary` per map type):
  - Resolves the table name, the identity key and the mapped columns. Every property with a setter is mapped; get-only properties are skipped, as are those listed with `Ignore`.
  - Validates table, schema and column names as plain identifiers, so they are safe to bracket-quote into SQL.
  - Gives each column an explicit `DbType` and size, and reads nullability from the C# declaration (`NullabilityInfoContext`).
  - Fails fast with clear messages: missing `ToTable` or `HasKey`, no parameterless constructor, unsupported property type ("use `StoredAsJson()`"), invalid property expression.
- [x] `SqlStatementBuilder`:
  - `Insert` excludes the identity key and returns `OUTPUT INSERTED.[Id]`.
  - `Update` covers every column except the key, insert-only columns (`CreatedBy`, `CreatedAt`) and the soft-delete flag, and only affects active rows (`AND [IsDeleted] = 0`).
  - `SelectById` and `SelectAll` add `[IsDeleted] = 0` when the entity has soft delete. `SelectAll` orders by key.
  - `SoftDelete` is an `UPDATE` that sets `[IsDeleted] = 1` (a constant, not a value read from the entity) plus `UpdatedBy` and `UpdatedAt` from the entity, on an active row.
  - Identifiers come only from metadata, bracket-quoted. Every value is a parameter with an explicit type and size.
- [x] `DataRecordMapper<T>`:
  - Maps by column name, ignoring case, and resolves positions once per reader. Unknown reader columns (for example `COUNT(*) OVER()`) are ignored, and missing columns keep their defaults.
  - Handles `DBNull` to `null` (a clear error if the property is not nullable), `Nullable<T>`, enums, `DateOnly` (from `DATE`), `DateTime` (read as **UTC**) and numeric widening.
  - JSON columns use `System.Text.Json`. An interface collection (`IReadOnlyList<string>`) is deserialized into an array.
  - Creates entities through their private constructor and sets private (and base-class `private protected`) setters through Reflection.
- [x] `SqlRepository<T>`:
  - `GetByIdAsync`, `ListAsync`, `InsertAsync` (sets the entity's key), `UpdateAsync` and `SoftDeleteAsync` (`false` when the row is missing or deleted).
  - Protected helpers: `QueryAsync` (with an optional `readRow` callback for extra columns such as a total count), `ExecuteScalarAsync<T>`, `Parameter(column, value, name)`, `SelectList` and `SelectFromTable`.
  - Async throughout and `CancellationToken`-aware. One pooled connection per operation.
  - Instrumented with `DataAccessInstrumentation` (T02.2): one span and one log per operation, with the table, id, row count and elapsed time, and never parameter values. Failures are logged and rethrown.
- [x] DI: `IDbConnectionFactory` is registered as `SqlConnectionFactory` (singleton). The concrete repositories are registered in T06.

## Tests (TDD)
- [x] Unit tests (no database), using a `Gadget` test entity that covers every feature:
  - `EntityMetadataTests`: table, key, columns, types and sizes, nullability, insert-only, JSON, soft delete, caching, the SuperHero and User maps, and invalid maps (missing table or key, injected identifiers, unsupported types, bad expressions).
  - `SqlStatementBuilderTests`: the exact SQL text and typed parameters for every statement. **Injection payloads in every text field of a real `SuperHero` never reach the SQL text.**
  - `DataRecordMapperTests` (`DataTable.CreateDataReader()`): private constructor and setters, nulls, UTC, case-insensitive names, extra and missing columns, conversion errors naming the column, numeric widening, the `User` entity including base-class audit properties.
- [x] Integration tests (Testcontainers, a fresh migrated and seeded database per test):
  - `SqlRepositoryTests`:
    - Reads the seeded heroes and Admin (proving the maps match the real schema).
    - The full lifecycle: insert, get, update (creation audit untouched), then soft delete (hidden but kept).
    - A deleted row can't be updated or deleted again.
    - A malicious value is stored as data.
    - SQL errors propagate (2601).
    - Custom queries, paging with `COUNT(*) OVER()`, and scalars.
    - **Every operation is logged once with its timing**, and failures are logged without values.
  - `SqlConnectionFactoryTests`: opens a connection, and fails clearly without a connection string.

## Done when
- [x] Mapper coverage is at least 90%: **97.5% line coverage** for `Mapping/`, `Maps/`, `Sql/` and `SqlRepository` (Microsoft code coverage, Cobertura). The connection factory's failed-open path is the main uncovered branch.
- [x] No string concatenation of user values exists in SQL: proven by `No_value_of_a_real_hero_ever_reaches_the_SQL_text` and `User_values_only_ever_appear_in_parameters`.
- [x] The Release build has 0 warnings. All 359 tests pass: Domain 120, Application 31, Infrastructure 173, ServiceDefaults 20 and Api 15.

## Implementation notes
- **Built on the ADO.NET base classes** (`DbConnection`, `DbCommand`, `DbParameter` with `DbType`) rather than `SqlParameter` and `SqlDbType`. Only `SqlConnectionFactory` knows the provider. `DbType` still gives SQL Server exact types (`String` becomes NVARCHAR, `AnsiString` VARCHAR, `AnsiStringFixedLength` CHAR, `DateTime2`, `Date`). The generated SQL is SQL Server dialect (brackets, `OUTPUT INSERTED`).
- **INSERT writes every non-key column**, including `CreatedAt`. The domain owns these values (the application clock, with time controllable in tests), so the database `DEFAULT`s only serve manual inserts. This replaces the plan's "exclude DB-defaulted columns".
- **Reflection use:** metadata (PropertyInfo, setter MethodInfo, ConstructorInfo, nullability) is reflected **once per map** and cached. Each row then costs one `ConstructorInfo.Invoke` plus one `MethodInfo.Invoke` per column, which is plenty for this application.
