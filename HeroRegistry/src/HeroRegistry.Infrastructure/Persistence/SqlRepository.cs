using System.Data;
using System.Data.Common;
using System.Globalization;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Mapping;
using HeroRegistry.Infrastructure.Persistence.Sql;

namespace HeroRegistry.Infrastructure.Persistence;

/// <summary>
/// Generic CRUD repository built on the Reflection mapper: no Entity Framework, Dapper or MediatR.
/// Every operation opens a pooled connection, runs one parameterized statement, and is timed, traced
/// and logged by <see cref="DataAccessInstrumentation"/> (T02.2). Concrete repositories (T06) add their
/// own queries with <see cref="QueryAsync"/> and <see cref="ExecuteScalarAsync{TValue}"/>.
/// </summary>
public abstract class SqlRepository<T>
    where T : class
{
    private readonly IDbConnectionFactory _connections;
    private readonly DataAccessInstrumentation _instrumentation;

    protected SqlRepository(IDbConnectionFactory connections, DataAccessInstrumentation instrumentation, EntityMap<T> map)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(instrumentation);
        ArgumentNullException.ThrowIfNull(map);

        _connections = connections;
        _instrumentation = instrumentation;
        Metadata = EntityMetadata.For(map);
    }

    protected EntityMetadata Metadata { get; }

    /// <summary>The mapped columns, for example <c>[Id], [HeroName], ...</c>, for custom SELECTs.</summary>
    protected string SelectList => SqlStatementBuilder.SelectList(Metadata);

    /// <summary><c>SELECT {columns} FROM [schema].[table]</c>, to append a WHERE clause to.</summary>
    protected string SelectFromTable => $"SELECT {SelectList} FROM {Metadata.QualifiedTableName}";

    /// <summary>The active row with this key, or <see langword="null"/>.</summary>
    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        InstrumentAsync(DataOperation.Select, id, async () =>
        {
            var rows = await ReadAsync(SqlStatementBuilder.SelectById(Metadata, id), null, cancellationToken);
            return (rows.Count == 0 ? null : rows[0], rows.Count, (object?)id);
        });

    /// <summary>Every active row, in key order.</summary>
    public Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        InstrumentAsync(DataOperation.Select, null, async () =>
        {
            var rows = await ReadAsync(SqlStatementBuilder.SelectAll(Metadata), null, cancellationToken);
            return (rows, rows.Count, (object?)null);
        });

    /// <summary>Inserts the entity, sets its key to the new identity and returns it.</summary>
    public Task<int> InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return InstrumentAsync(DataOperation.Insert, null, async () =>
        {
            var newId = await ScalarAsync(SqlStatementBuilder.Insert(Metadata, entity), cancellationToken);
            var id = Convert.ToInt32(newId, CultureInfo.InvariantCulture);
            Metadata.Key.SetValue(entity, ValueConverter.FromDatabase(Metadata.Key, id));
            return (id, 1, (object?)id);
        });
    }

    /// <summary>Saves the entity's changes. Returns <see langword="false"/> if the row is missing or deleted.</summary>
    public Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return InstrumentAsync(DataOperation.Update, KeyOf(entity), async () =>
        {
            var rows = await NonQueryAsync(SqlStatementBuilder.Update(Metadata, entity), cancellationToken);
            return (rows == 1, rows, (object?)null);
        });
    }

    /// <summary>
    /// Soft-deletes the row, using the audit values already set on the entity (call its SoftDelete first).
    /// Returns <see langword="false"/> if the row is missing or already deleted.
    /// </summary>
    public Task<bool> SoftDeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return InstrumentAsync(DataOperation.SoftDelete, KeyOf(entity), async () =>
        {
            var rows = await NonQueryAsync(SqlStatementBuilder.SoftDelete(Metadata, entity), cancellationToken);
            return (rows == 1, rows, (object?)null);
        });
    }

    /// <summary>
    /// Runs a custom SELECT and maps its rows to <typeparamref name="T"/>. Build the SQL from
    /// <see cref="SelectFromTable"/> and pass every value as a parameter (see <see cref="Parameter"/>).
    /// </summary>
    /// <param name="readRow">Optional: reads extra columns of each row, for example a COUNT(*) OVER() total.</param>
    protected Task<IReadOnlyList<T>> QueryAsync(
        string sql,
        IReadOnlyList<SqlParameterValue> parameters,
        CancellationToken cancellationToken,
        Action<IDataRecord>? readRow = null) =>
        InstrumentAsync(DataOperation.Select, null, async () =>
        {
            var rows = await ReadAsync(new SqlStatement(sql, parameters), readRow, cancellationToken);
            return (rows, rows.Count, (object?)null);
        });

    /// <summary>Runs a custom query that returns a single value (for example a COUNT or an EXISTS).</summary>
    protected Task<TValue?> ExecuteScalarAsync<TValue>(
        string sql,
        IReadOnlyList<SqlParameterValue> parameters,
        CancellationToken cancellationToken) =>
        InstrumentAsync(DataOperation.Select, null, async () =>
        {
            var value = await ScalarAsync(new SqlStatement(sql, parameters), cancellationToken);
            var result = value is null or DBNull ? default : (TValue)Convert.ChangeType(value, typeof(TValue), CultureInfo.InvariantCulture);
            return (result, value is null or DBNull ? 0 : 1, (object?)null);
        });

    /// <summary>A typed parameter for one of the entity's columns, for custom queries.</summary>
    /// <param name="columnName">The column (case-insensitive).</param>
    /// <param name="value">The property value; it is converted like the column's own values.</param>
    /// <param name="parameterName">For example <c>@Search</c>. Defaults to <c>@ColumnName</c>.</param>
    protected SqlParameterValue Parameter(string columnName, object? value, string? parameterName = null)
    {
        var column = Metadata.FindColumn(columnName) ?? throw new ArgumentException(
            $"{Metadata.EntityType.Name} has no column '{columnName}'.", nameof(columnName));
        return SqlStatementBuilder.Parameter(column, value, parameterName);
    }

    private object? KeyOf(T entity) => Metadata.Key.GetValue(entity);

    private async Task<TResult> InstrumentAsync<TResult>(
        DataOperation operation,
        object? entityId,
        Func<Task<(TResult Result, int RowCount, object? EntityId)>> work)
    {
        using var scope = _instrumentation.Begin(operation, Metadata.TableName, entityId);
        try
        {
            var (result, rowCount, id) = await work();
            scope.Succeeded(rowCount, id);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            scope.Failed(ex);
            throw;
        }
    }

    private async Task<IReadOnlyList<T>> ReadAsync(
        SqlStatement statement,
        Action<IDataRecord>? readRow,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, statement);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<T>();
        DataRecordMapper<T>? mapper = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            mapper ??= new DataRecordMapper<T>(Metadata, reader);
            rows.Add(mapper.Map(reader));
            readRow?.Invoke(reader);
        }

        return rows;
    }

    private async Task<object?> ScalarAsync(SqlStatement statement, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, statement);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<int> NonQueryAsync(SqlStatement statement, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, statement);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(DbConnection connection, SqlStatement statement)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement.Sql;
        command.AddParameters(statement.Parameters);
        return command;
    }
}
