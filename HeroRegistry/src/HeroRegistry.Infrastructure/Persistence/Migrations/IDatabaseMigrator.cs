namespace HeroRegistry.Infrastructure.Persistence.Migrations;

/// <summary>Brings the database schema up to date with the embedded migration scripts.</summary>
public interface IDatabaseMigrator
{
    Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default);
}

/// <param name="AppliedScripts">Scripts applied by this run (empty when already up to date).</param>
/// <param name="TotalScripts">Number of scripts known to this version of the application.</param>
public sealed record MigrationResult(IReadOnlyList<string> AppliedScripts, int TotalScripts);
