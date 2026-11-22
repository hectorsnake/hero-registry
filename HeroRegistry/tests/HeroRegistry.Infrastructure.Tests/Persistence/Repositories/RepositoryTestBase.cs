using HeroRegistry.Domain.Heroes;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Repositories;
using HeroRegistry.Infrastructure.Persistence.Sql;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// A freshly migrated and seeded database per test (Admin, Captain America, Spider-Man, Superman),
/// with the real repositories on top of it.
/// </summary>
public abstract class RepositoryTestBase(SqlServerContainerFixture sqlServer) : IAsyncLifetime
{
    /// <summary>Later than the seed data, so new rows sort last by CreatedAt.</summary>
    protected static readonly DateTime Now = DateTime.UtcNow.AddDays(1);

    protected TestDatabase Database { get; } = new(sqlServer);

    protected HeroRepository Heroes { get; private set; } = null!;

    protected UserRepository Users { get; private set; } = null!;

    protected RefreshTokenRepository RefreshTokens { get; private set; } = null!;

    protected int AdminId { get; private set; }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await Database.MigrateAsync();

        var connections = new SqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Database.DatabaseConnectionString }));
        var instrumentation = new DataAccessInstrumentation(
            NullLogger<DataAccessInstrumentation>.Instance, Options.Create(new DatabaseOptions()), TimeProvider.System);

        Heroes = new HeroRepository(connections, instrumentation);
        Users = new UserRepository(connections, instrumentation);
        RefreshTokens = new RefreshTokenRepository(connections, instrumentation);
        AdminId = await Database.ScalarAsync<int>("SELECT Id FROM dbo.Users WHERE UserName = 'Admin'");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task<int> SeededHeroIdAsync(string heroName) =>
        await Database.ScalarAsync<int>("SELECT Id FROM dbo.SuperHeroes WHERE HeroName = @Name", ("@Name", heroName));

    protected SuperHero NewHero(
        string heroName,
        string? email = null,
        int powerLevel = 50,
        string city = "Manhattan",
        string[]? superpowers = null) =>
        SuperHero.Create(
            new SuperHeroDetails(
                heroName, "Test", "Hero", new DateOnly(1990, 1, 1),
                email ?? $"{Guid.NewGuid():N}@heroregistry.com",
                superpowers ?? ["Testing"], powerLevel,
                "1 Test Street", null, city, "NY", "10001"),
            AdminId,
            Now);
}
