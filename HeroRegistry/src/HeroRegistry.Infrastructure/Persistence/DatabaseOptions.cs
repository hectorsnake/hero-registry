namespace HeroRegistry.Infrastructure.Persistence;

/// <summary>
/// Database settings, bound from the <c>Database</c> configuration section.
/// The connection string comes from <c>ConnectionStrings:HeroRegistryDb</c>, which Aspire (development)
/// and Docker Compose (delivery) both provide through configuration.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public const string ConnectionStringName = "HeroRegistryDb";

    /// <summary>Configuration key for <see cref="SlowCommandThresholdMs"/>.</summary>
    public const string SlowCommandThresholdKey = "Database:SlowCommandThresholdMs";

    public const int DefaultSlowCommandThresholdMs = 500;

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Operations slower than this are logged as warnings. 0 disables the warning.
    /// </summary>
    public int SlowCommandThresholdMs { get; set; } = DefaultSlowCommandThresholdMs;

    /// <summary>Apply pending migration scripts when the API starts. Default: true.</summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Attempts while SQL Server is not reachable yet (for example still starting). Default: 10.</summary>
    public int MigrationMaxAttempts { get; set; } = 10;

    /// <summary>Wait between attempts. Default: 3 seconds.</summary>
    public TimeSpan MigrationRetryDelay { get; set; } = TimeSpan.FromSeconds(3);
}
