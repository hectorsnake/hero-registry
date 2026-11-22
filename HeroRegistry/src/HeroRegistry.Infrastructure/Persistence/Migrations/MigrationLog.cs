using Microsoft.Extensions.Logging;

namespace HeroRegistry.Infrastructure.Persistence.Migrations;

internal static partial class MigrationLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Created database {DatabaseName}")]
    public static partial void DatabaseCreated(ILogger logger, string databaseName);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Applying database migration {ScriptName}")]
    public static partial void Applying(ILogger logger, string scriptName);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Database {DatabaseName} is up to date ({AppliedCount} of {TotalCount} migration scripts applied by this run)")]
    public static partial void UpToDate(ILogger logger, string databaseName, int appliedCount, int totalCount);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "Database migration attempt {Attempt} of {MaxAttempts} failed ({ExceptionType}, SQL error {SqlErrorNumber}); retrying in {RetryDelaySeconds} s")]
    public static partial void RetryingAfterFailure(
        ILogger logger, int attempt, int maxAttempts, string exceptionType, int? sqlErrorNumber, double retryDelaySeconds);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning,
        Message = "Database migrations skipped: connection string '{ConnectionStringName}' is not configured")]
    public static partial void SkippedNoConnectionString(ILogger logger, string connectionStringName);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information,
        Message = "Database migrations on startup are disabled (Database:MigrateOnStartup = false)")]
    public static partial void SkippedDisabled(ILogger logger);
}
