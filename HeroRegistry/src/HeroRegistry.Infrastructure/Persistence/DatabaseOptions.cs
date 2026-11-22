namespace HeroRegistry.Infrastructure.Persistence;

/// <summary>
/// Database settings. The connection string comes from <c>ConnectionStrings:HeroRegistryDb</c>,
/// which Aspire (development) and Docker Compose (delivery) both provide through configuration.
/// </summary>
public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "HeroRegistryDb";

    public string ConnectionString { get; set; } = string.Empty;
}
