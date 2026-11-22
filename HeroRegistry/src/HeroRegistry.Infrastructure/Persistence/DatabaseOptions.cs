namespace HeroRegistry.Infrastructure.Persistence;

/// <summary>
/// Database settings. The connection string comes from <c>ConnectionStrings:HeroRegistryDb</c>,
/// which Aspire (development) and Docker Compose (delivery) both provide through configuration.
/// </summary>
public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "HeroRegistryDb";

    /// <summary>Configuration key for <see cref="SlowCommandThresholdMs"/>.</summary>
    public const string SlowCommandThresholdKey = "Database:SlowCommandThresholdMs";

    public const int DefaultSlowCommandThresholdMs = 500;

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Operations slower than this are logged as warnings. 0 disables the warning.
    /// </summary>
    public int SlowCommandThresholdMs { get; set; } = DefaultSlowCommandThresholdMs;
}
