using Microsoft.Extensions.Logging;

namespace HeroRegistry.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// Source-generated, structured log messages for the data layer (Application Insights <c>traces</c>).
/// Only tables, ids, counts and timings are logged. Parameter values and exception messages are never
/// logged, because they can contain personal data.
/// </summary>
internal static partial class DataAccessLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Inserted {Table} row {EntityId} in {ElapsedMs} ms")]
    public static partial void Inserted(ILogger logger, string table, string? entityId, double elapsedMs);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "Updated {Table} row {EntityId} ({RowsAffected} row(s) affected) in {ElapsedMs} ms")]
    public static partial void Updated(ILogger logger, string table, string? entityId, int rowsAffected, double elapsedMs);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information,
        Message = "Soft-deleted {Table} row {EntityId} ({RowsAffected} row(s) affected) in {ElapsedMs} ms")]
    public static partial void SoftDeleted(ILogger logger, string table, string? entityId, int rowsAffected, double elapsedMs);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Debug,
        Message = "Selected {RowCount} {Table} rows in {ElapsedMs} ms")]
    public static partial void Selected(ILogger logger, int rowCount, string table, double elapsedMs);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning,
        Message = "Slow {Operation} on {Table}: {ElapsedMs} ms (threshold {ThresholdMs} ms)")]
    public static partial void SlowOperation(ILogger logger, string operation, string table, double elapsedMs, int thresholdMs);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Error,
        Message = "{Operation} on {Table} failed after {ElapsedMs} ms: {ExceptionType} (SQL error {SqlErrorNumber})")]
    public static partial void Failed(ILogger logger, string operation, string table, double elapsedMs, string exceptionType, int? sqlErrorNumber);
}
