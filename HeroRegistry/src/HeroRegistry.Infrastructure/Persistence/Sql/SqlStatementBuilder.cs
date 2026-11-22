using HeroRegistry.Infrastructure.Persistence.Mapping;

namespace HeroRegistry.Infrastructure.Persistence.Sql;

/// <summary>
/// Generates parameterized SQL Server statements from <see cref="EntityMetadata"/>. Identifiers come only
/// from the (validated, bracket-quoted) metadata and every value is a parameter, so user input can never
/// change the SQL text.
/// </summary>
public static class SqlStatementBuilder
{
    /// <summary>The mapped columns, for example <c>[Id], [HeroName], ...</c>.</summary>
    public static string SelectList(EntityMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return string.Join(", ", metadata.Columns.Select(c => c.QuotedName));
    }

    /// <summary>One active row by key.</summary>
    public static SqlStatement SelectById(EntityMetadata metadata, object id)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var sql = $"SELECT {SelectList(metadata)} FROM {metadata.QualifiedTableName} " +
                  $"WHERE {metadata.Key.QuotedName} = {metadata.Key.ParameterName}{ActiveRowsFilter(metadata, " AND ")}";

        return new SqlStatement(sql, [Parameter(metadata.Key, id)]);
    }

    /// <summary>Every active row, in key order.</summary>
    public static SqlStatement SelectAll(EntityMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var sql = $"SELECT {SelectList(metadata)} FROM {metadata.QualifiedTableName}" +
                  $"{ActiveRowsFilter(metadata, " WHERE ")} ORDER BY {metadata.Key.QuotedName}";

        return new SqlStatement(sql, []);
    }

    /// <summary>Inserts every column except the identity key and returns the new key (OUTPUT INSERTED).</summary>
    public static SqlStatement Insert(EntityMetadata metadata, object entity)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(entity);

        var columns = metadata.Columns.Where(c => !c.IsKey).ToList();

        var sql = $"INSERT INTO {metadata.QualifiedTableName} ({string.Join(", ", columns.Select(c => c.QuotedName))}) " +
                  $"OUTPUT INSERTED.{metadata.Key.QuotedName} " +
                  $"VALUES ({string.Join(", ", columns.Select(c => c.ParameterName))})";

        return new SqlStatement(sql, columns.Select(c => Parameter(c, c.GetValue(entity))).ToList());
    }

    /// <summary>
    /// Updates an active row: every column except the key, insert-only columns (creation audit) and the
    /// soft-delete flag (deleting is <see cref="SoftDelete"/>'s job). Affects 0 rows if the row is deleted.
    /// </summary>
    public static SqlStatement Update(EntityMetadata metadata, object entity)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(entity);

        var columns = metadata.Columns.Where(c => !c.IsKey && !c.IsInsertOnly && !c.IsSoftDeleteFlag).ToList();

        var sql = $"UPDATE {metadata.QualifiedTableName} " +
                  $"SET {string.Join(", ", columns.Select(c => $"{c.QuotedName} = {c.ParameterName}"))} " +
                  $"WHERE {metadata.Key.QuotedName} = {metadata.Key.ParameterName}{ActiveRowsFilter(metadata, " AND ")}";

        var parameters = columns.Select(c => Parameter(c, c.GetValue(entity)))
            .Append(Parameter(metadata.Key, metadata.Key.GetValue(entity)))
            .ToList();

        return new SqlStatement(sql, parameters);
    }

    /// <summary>
    /// Flags an active row as deleted and sets the soft-delete audit columns from the entity
    /// (call the entity's own SoftDelete first). Affects 0 rows if the row was already deleted.
    /// </summary>
    public static SqlStatement SoftDelete(EntityMetadata metadata, object entity)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(entity);

        var flag = metadata.SoftDeleteFlag ?? throw new InvalidOperationException(
            $"{metadata.EntityType.Name} has no soft-delete mapping (HasSoftDelete).");

        // The flag is a constant (1), never a value read from the entity.
        var assignments = new[] { $"{flag.QuotedName} = 1" }
            .Concat(metadata.SoftDeleteAlsoSets.Select(c => $"{c.QuotedName} = {c.ParameterName}"));

        var sql = $"UPDATE {metadata.QualifiedTableName} SET {string.Join(", ", assignments)} " +
                  $"WHERE {metadata.Key.QuotedName} = {metadata.Key.ParameterName} AND {flag.QuotedName} = 0";

        var parameters = metadata.SoftDeleteAlsoSets.Select(c => Parameter(c, c.GetValue(entity)))
            .Append(Parameter(metadata.Key, metadata.Key.GetValue(entity)))
            .ToList();

        return new SqlStatement(sql, parameters);
    }

    /// <summary>A typed parameter for a column, with the value converted for the database.</summary>
    /// <param name="parameterName">Defaults to the column's parameter name, for example <c>@HeroName</c>.</param>
    public static SqlParameterValue Parameter(ColumnMetadata column, object? value, string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(column);

        return new SqlParameterValue(
            parameterName ?? column.ParameterName,
            ValueConverter.ToDatabase(column, value),
            column.DbType,
            column.Size);
    }

    /// <summary>"[IsDeleted] = 0" for soft-deleted entities, with the given prefix; otherwise nothing.</summary>
    internal static string ActiveRowsFilter(EntityMetadata metadata, string prefix) =>
        metadata.SoftDeleteFlag is { } flag ? $"{prefix}{flag.QuotedName} = 0" : string.Empty;
}
