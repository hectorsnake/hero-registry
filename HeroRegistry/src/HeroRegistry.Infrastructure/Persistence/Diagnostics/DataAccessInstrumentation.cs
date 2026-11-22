using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// The data layer's timer. Each repository operation (SELECT, INSERT, UPDATE, soft DELETE) is:
/// <list type="bullet">
///   <item>timed, using <see cref="TimeProvider"/> so tests can control time;</item>
///   <item>traced as one span from the <c>HeroRegistry.Data</c> ActivitySource (Application Insights
///   <c>dependencies</c>, parent of the SqlClient span);</item>
///   <item>logged once through <see cref="DataAccessLog"/> (Application Insights <c>traces</c>).</item>
/// </list>
/// Usage in a repository (T05, T06):
/// <code>
/// using var operation = instrumentation.Begin(DataOperation.Insert, "SuperHeroes");
/// try
/// {
///     var id = await ExecuteInsertAsync(...);
///     operation.Succeeded(rowCount: 1, entityId: id);
/// }
/// catch (Exception ex)
/// {
///     operation.Failed(ex);
///     throw;
/// }
/// </code>
/// </summary>
public sealed class DataAccessInstrumentation(
    ILogger<DataAccessInstrumentation> logger,
    IOptions<DatabaseOptions> options,
    TimeProvider timeProvider)
{
    public const string ActivitySourceName = "HeroRegistry.Data";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public DataOperationScope Begin(DataOperation operation, string table, object? entityId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var id = FormatId(entityId);
        var activity = ActivitySource.StartActivity($"{operation.DisplayName()} {table}", ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag("db.system.name", "microsoft.sql_server");
            activity.SetTag("db.operation.name", operation.SqlStatement());
            activity.SetTag("db.collection.name", table);

            if (id is not null)
            {
                activity.SetTag("heroregistry.entity.id", id);
            }
        }

        return new DataOperationScope(this, operation, table, id, activity, timeProvider.GetTimestamp());
    }

    internal double ElapsedMilliseconds(long startTimestamp) =>
        Math.Round(timeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds, 2);

    internal void LogSucceeded(DataOperation operation, string table, string? entityId, int rowCount, double elapsedMs)
    {
        switch (operation)
        {
            case DataOperation.Select:
                DataAccessLog.Selected(logger, rowCount, table, elapsedMs);
                break;
            case DataOperation.Insert:
                DataAccessLog.Inserted(logger, table, entityId, elapsedMs);
                break;
            case DataOperation.Update:
                DataAccessLog.Updated(logger, table, entityId, rowCount, elapsedMs);
                break;
            case DataOperation.SoftDelete:
                DataAccessLog.SoftDeleted(logger, table, entityId, rowCount, elapsedMs);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }

        var thresholdMs = options.Value.SlowCommandThresholdMs;
        if (thresholdMs > 0 && elapsedMs > thresholdMs)
        {
            DataAccessLog.SlowOperation(logger, operation.DisplayName(), table, elapsedMs, thresholdMs);
        }
    }

    internal void LogFailed(DataOperation operation, string table, double elapsedMs, Exception exception, int? sqlErrorNumber) =>
        // Only the exception type and SQL error number: the message can contain personal data.
        DataAccessLog.Failed(logger, operation.DisplayName(), table, elapsedMs, exception.GetType().FullName!, sqlErrorNumber);

    internal static string? FormatId(object? entityId) =>
        entityId is null ? null : Convert.ToString(entityId, CultureInfo.InvariantCulture);
}
