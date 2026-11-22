using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace HeroRegistry.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// One timed database operation, created by <see cref="DataAccessInstrumentation.Begin"/>.
/// Call <see cref="Succeeded"/> or <see cref="Failed"/> exactly once, then dispose it.
/// </summary>
public sealed class DataOperationScope : IDisposable
{
    private readonly DataAccessInstrumentation _instrumentation;
    private readonly DataOperation _operation;
    private readonly string _table;
    private readonly Activity? _activity;
    private readonly long _startTimestamp;
    private string? _entityId;
    private bool _completed;

    internal DataOperationScope(
        DataAccessInstrumentation instrumentation,
        DataOperation operation,
        string table,
        string? entityId,
        Activity? activity,
        long startTimestamp)
    {
        _instrumentation = instrumentation;
        _operation = operation;
        _table = table;
        _entityId = entityId;
        _activity = activity;
        _startTimestamp = startTimestamp;
    }

    /// <param name="rowCount">Rows returned (SELECT) or affected (INSERT, UPDATE, soft DELETE).</param>
    /// <param name="entityId">The row's key, when it is only known afterwards (for example a new identity).</param>
    public void Succeeded(int rowCount, object? entityId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        Complete();

        var elapsedMs = _instrumentation.ElapsedMilliseconds(_startTimestamp);
        _entityId = DataAccessInstrumentation.FormatId(entityId) ?? _entityId;

        if (_activity is not null)
        {
            if (_entityId is not null)
            {
                _activity.SetTag("heroregistry.entity.id", _entityId);
            }

            _activity.SetTag(
                _operation == DataOperation.Select ? "db.response.returned_rows" : "heroregistry.rows_affected",
                rowCount);
        }

        _instrumentation.LogSucceeded(_operation, _table, _entityId, rowCount, elapsedMs);
    }

    public void Failed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Complete();

        var elapsedMs = _instrumentation.ElapsedMilliseconds(_startTimestamp);
        var sqlErrorNumber = (exception as SqlException)?.Number;

        if (_activity is not null)
        {
            // The exception itself is not attached: its message can contain personal data.
            _activity.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            _activity.SetTag("error.type", exception.GetType().FullName);

            if (sqlErrorNumber is not null)
            {
                _activity.SetTag("db.response.status_code", sqlErrorNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        _instrumentation.LogFailed(_operation, _table, elapsedMs, exception, sqlErrorNumber);
    }

    public void Dispose()
    {
        if (!_completed)
        {
            // Neither Succeeded nor Failed was called: an exception escaped the repository code.
            _activity?.SetStatus(ActivityStatusCode.Error, "Operation did not complete");
            _completed = true;
        }

        _activity?.Dispose();
    }

    private void Complete()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The database operation has already completed.");
        }

        _completed = true;
    }
}
