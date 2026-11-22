# T05: Reflection-based data mapper

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Infrastructure · **Estimate:** 8 h · **Depends on:** T03, T04

## Goal
Build a small, generic, **Reflection-based** persistence engine on top of ADO.NET (`Microsoft.Data.SqlClient`) that maps objects to table rows and back. Entity Framework, Dapper and MediatR are not allowed.

## Design
```
Persistence/
├── Mapping/
│   ├── Attributes: TableAttribute, KeyAttribute, ColumnAttribute,
│   │               NotMappedAttribute, JsonColumnAttribute, SoftDeleteAttribute
│   ├── EntityMetadata<T>      # PropertyInfo cache, built once per type
│   └── DataRecordMapper       # IDataRecord to T
├── Sql/
│   └── SqlStatementBuilder    # parameterized SELECT/INSERT/UPDATE/soft DELETE
├── ISqlConnectionFactory / SqlConnectionFactory
└── SqlRepository<T>           # generic CRUD base
```

To keep Domain free of persistence concerns, the mapping is configured in Infrastructure. Use either attributes on internal persistence records or a fluent `EntityMap<T>` registration. Choose one and document it.

## Steps
- [ ] `EntityMetadata<T>`:
  - Resolves the table name, key property (identity) and mapped columns.
  - Skips `[NotMapped]`.
  - Caches results in a `static readonly` or `ConcurrentDictionary<Type, ...>`.
- [ ] `SqlStatementBuilder`:
  - `Insert` returns `OUTPUT INSERTED.Id` and excludes identity and DB-defaulted columns.
  - `Update` covers all columns except key and create-audit columns.
  - `SelectById` and `SelectAll` automatically add `WHERE IsDeleted = 0` when the entity supports soft delete.
  - `SoftDelete` is an `UPDATE` that sets `IsDeleted = 1` and `UpdatedBy`/`UpdatedAt`.
  - Identifiers are bracket-quoted from metadata only. All values are `SqlParameter`s with explicit `SqlDbType` and size.
- [ ] `DataRecordMapper`:
  - Maps by column name (case-insensitive) and caches the ordinal per reader.
  - Handles `DBNull` to `null`, `Nullable<T>`, enums and `DateOnly` (DATE).
  - `[JsonColumn]` values are converted with `System.Text.Json` (`string[]` to and from `NVARCHAR(MAX)`).
  - Sets private setters through Reflection.
- [ ] `SqlRepository<T>`:
  - `GetByIdAsync`, `ListAsync`, `InsertAsync`, `UpdateAsync`, `SoftDeleteAsync`
  - Protected helpers `QueryAsync<T>(sql, params)` and `ExecuteScalarAsync` for custom queries
  - Async all the way and `CancellationToken`-aware
  - Instrumented with the `HeroRegistry.Data` `ActivitySource` and `DataAccessLog` from [T02.2](T02.2-Application-Insights-Logging.md): one span and one log per operation, with table, id, row count and elapsed time, and never parameter values

## Tests (TDD)
- [ ] Unit tests (no database):
  - Metadata resolution and caching (same instance returned).
  - The generated SQL text and parameters for each statement type.
  - Malicious values such as `"'; DROP TABLE Users;--"` end up only in parameters.
  - Mapping from a fake `IDataRecord` (`DataTable.CreateDataReader()`), including nulls, `DateOnly` and the JSON array.
- [ ] Integration tests (Testcontainers):
  - Insert, then get by id, then update, then soft delete, then get by id returns null, for a test entity.

## Done when
- Mapper coverage is at least 90%, and no string concatenation of user values exists anywhere in SQL.
