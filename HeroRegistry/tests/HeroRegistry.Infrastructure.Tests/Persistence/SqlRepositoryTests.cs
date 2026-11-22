using HeroRegistry.Domain.Heroes;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Maps;
using HeroRegistry.Infrastructure.Persistence.Sql;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the generic repository against a real SQL Server (Testcontainers), using the
/// real domain entities and maps. Each test gets a freshly migrated and seeded database.
/// </summary>
public sealed class SqlRepositoryTests(SqlServerContainerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateTime CreatedAt = new(2026, 11, 22, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new(sqlServer);
    private readonly FakeLogger<DataAccessInstrumentation> _logger = new();
    private HeroTestRepository _heroes = null!;
    private UserTestRepository _users = null!;
    private int _adminId;

    public async ValueTask InitializeAsync()
    {
        await _database.MigrateAsync();

        var connections = new SqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = _database.DatabaseConnectionString }));
        var instrumentation = new DataAccessInstrumentation(
            _logger, Options.Create(new DatabaseOptions()), TimeProvider.System);

        _heroes = new HeroTestRepository(connections, instrumentation);
        _users = new UserTestRepository(connections, instrumentation);
        _adminId = await _database.ScalarAsync<int>("SELECT Id FROM dbo.Users WHERE UserName = 'Admin'");
        _logger.Collector.Clear();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Reading the seeded data proves the maps match the real schema.

    [Fact]
    public async Task ListAsync_loads_the_seeded_heroes_with_every_column()
    {
        var heroes = await _heroes.ListAsync(Ct);

        heroes.Select(h => h.HeroName).ShouldBe(["Captain America", "Spider-Man", "Superman"]);

        var superman = heroes.Single(h => h.HeroName == "Superman");
        superman.FirstName.ShouldBe("Clark");
        superman.BirthDate.ShouldBe(new DateOnly(1977, 6, 18));
        superman.Superpowers.ShouldBe(["Flight", "Super strength", "Heat vision", "Invulnerability", "X-ray vision"]);
        superman.PowerLevel.ShouldBe(100);
        superman.Address2.ShouldBe("Apt 3D");
        superman.State.ShouldBe("NY");
        superman.CreatedBy.ShouldBe(_adminId);
        superman.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        superman.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_loads_the_seeded_administrator()
    {
        var admin = await _users.GetByIdAsync(_adminId, Ct);

        admin.ShouldNotBeNull();
        admin.UserName.ShouldBe("Admin");
        admin.Email.ShouldBe("admin@heroregistry.com");
        admin.IsAdmin.ShouldBeTrue();
        admin.Password.ShouldStartWith("$2a$11$");
        admin.CreatedBy.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_id()
    {
        (await _heroes.GetByIdAsync(999_999, Ct)).ShouldBeNull();
    }

    // Full lifecycle: insert, read, update, soft delete

    [Fact]
    public async Task InsertAsync_stores_the_entity_and_assigns_its_identity()
    {
        var hero = NewHero("Black Widow", "black.widow@heroregistry.com");

        var id = await _heroes.InsertAsync(hero, Ct);

        id.ShouldBeGreaterThan(0);
        hero.Id.ShouldBe(id);

        var loaded = (await _heroes.GetByIdAsync(id, Ct)).ShouldNotBeNull();
        loaded.HeroName.ShouldBe("Black Widow");
        loaded.Superpowers.ShouldBe(["Espionage", "Martial arts"]);
        loaded.BirthDate.ShouldBe(new DateOnly(1984, 11, 22));
        loaded.Address2.ShouldBeNull();
        loaded.CreatedBy.ShouldBe(_adminId);
        loaded.CreatedAt.ShouldBe(CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_saves_the_changes_but_never_the_creation_audit()
    {
        var hero = NewHero("Hawkeye", "hawkeye@heroregistry.com");
        await _heroes.InsertAsync(hero, Ct);

        var loaded = (await _heroes.GetByIdAsync(hero.Id, Ct))!;
        var updatedAt = CreatedAt.AddHours(1);
        loaded.Update(Details("Hawkeye", "hawkeye@heroregistry.com") with { PowerLevel = 70, City = "Bedford-Stuyvesant" },
            _adminId, updatedAt);

        (await _heroes.UpdateAsync(loaded, Ct)).ShouldBeTrue();

        var reloaded = (await _heroes.GetByIdAsync(hero.Id, Ct))!;
        reloaded.PowerLevel.ShouldBe(70);
        reloaded.City.ShouldBe("Bedford-Stuyvesant");
        reloaded.UpdatedBy.ShouldBe(_adminId);
        reloaded.UpdatedAt.ShouldBe(updatedAt);
        reloaded.CreatedAt.ShouldBe(CreatedAt);
    }

    [Fact]
    public async Task SoftDeleteAsync_hides_the_row_but_keeps_it_in_the_table()
    {
        var hero = NewHero("Vision", "vision@heroregistry.com");
        await _heroes.InsertAsync(hero, Ct);
        hero.SoftDelete(_adminId, CreatedAt.AddDays(1));

        (await _heroes.SoftDeleteAsync(hero, Ct)).ShouldBeTrue();

        (await _heroes.GetByIdAsync(hero.Id, Ct)).ShouldBeNull();
        (await _heroes.ListAsync(Ct)).ShouldNotContain(h => h.Id == hero.Id);
        (await _database.ScalarAsync<bool>("SELECT IsDeleted FROM dbo.SuperHeroes WHERE Id = @Id", ("@Id", hero.Id)))
            .ShouldBeTrue();
        (await _database.ScalarAsync<int?>("SELECT UpdatedBy FROM dbo.SuperHeroes WHERE Id = @Id", ("@Id", hero.Id)))
            .ShouldBe(_adminId);
    }

    [Fact]
    public async Task A_deleted_row_cannot_be_updated_or_deleted_again()
    {
        var hero = NewHero("Quicksilver", "quicksilver@heroregistry.com");
        await _heroes.InsertAsync(hero, Ct);
        var stale = (await _heroes.GetByIdAsync(hero.Id, Ct))!;
        hero.SoftDelete(_adminId, CreatedAt.AddDays(1));
        await _heroes.SoftDeleteAsync(hero, Ct);

        stale.Update(Details("Quicksilver", "quicksilver@heroregistry.com"), _adminId, CreatedAt.AddDays(2));

        (await _heroes.UpdateAsync(stale, Ct)).ShouldBeFalse();
        (await _heroes.SoftDeleteAsync(stale, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Users_can_be_inserted_and_read_back()
    {
        var user = User.Register("peter.parker", "peter@dailybugle.com", "$2a$11$hash", CreatedAt);

        await _users.InsertAsync(user, Ct);

        var loaded = (await _users.GetByIdAsync(user.Id, Ct)).ShouldNotBeNull();
        loaded.UserName.ShouldBe("peter.parker");
        loaded.IsAdmin.ShouldBeFalse();
    }

    // Safety

    [Fact]
    public async Task Malicious_values_are_stored_as_plain_data()
    {
        const string malicious = "'; DROP TABLE Users;--";
        var hero = NewHero(malicious, "bobby.tables@heroregistry.com");

        await _heroes.InsertAsync(hero, Ct);

        (await _heroes.GetByIdAsync(hero.Id, Ct))!.HeroName.ShouldBe(malicious);
        (await _database.ScalarAsync<int?>("SELECT OBJECT_ID('dbo.Users', 'U')")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Database_errors_propagate_to_the_caller()
    {
        // Same hero name as a seeded hero: the unique index rejects it. T06 translates this error.
        var exception = await Should.ThrowAsync<SqlException>(
            () => _heroes.InsertAsync(NewHero("Superman", "another.superman@heroregistry.com"), Ct));

        exception.Number.ShouldBe(2601);
    }

    // Custom queries for the concrete repositories (T06)

    [Fact]
    public async Task QueryAsync_maps_a_custom_parameterized_query()
    {
        var heroes = await _heroes.FindByCityAsync("Queens", Ct);

        heroes.ShouldHaveSingleItem().HeroName.ShouldBe("Spider-Man");
    }

    [Fact]
    public async Task QueryAsync_can_read_extra_columns_such_as_a_total_count()
    {
        var (page, total) = await _heroes.FirstPageAsync(pageSize: 2, Ct);

        page.Count.ShouldBe(2);
        total.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteScalarAsync_returns_a_single_value()
    {
        (await _heroes.CountActiveAsync(Ct)).ShouldBe(3);
    }

    // Instrumentation (T02.2): every operation is timed and logged once

    [Fact]
    public async Task Each_operation_is_timed_and_logged()
    {
        var hero = NewHero("Falcon", "falcon@heroregistry.com");
        await _heroes.InsertAsync(hero, Ct);
        await _heroes.GetByIdAsync(hero.Id, Ct);
        hero.Update(Details("Falcon", "falcon@heroregistry.com") with { PowerLevel = 60 }, _adminId, CreatedAt.AddHours(1));
        await _heroes.UpdateAsync(hero, Ct);
        hero.SoftDelete(_adminId, CreatedAt.AddHours(2));
        await _heroes.SoftDeleteAsync(hero, Ct);

        _logger.Collector.GetSnapshot().Select(r => r.Message).ShouldBe(
        [
            $"Inserted SuperHeroes row {hero.Id} in {Elapsed(0)} ms",
            $"Selected 1 SuperHeroes rows in {Elapsed(1)} ms",
            $"Updated SuperHeroes row {hero.Id} (1 row(s) affected) in {Elapsed(2)} ms",
            $"Soft-deleted SuperHeroes row {hero.Id} (1 row(s) affected) in {Elapsed(3)} ms",
        ]);
    }

    [Fact]
    public async Task Failed_operations_are_logged_as_errors()
    {
        await Should.ThrowAsync<SqlException>(
            () => _heroes.InsertAsync(NewHero("Superman", "another.superman@heroregistry.com"), Ct));

        var record = _logger.Collector.LatestRecord;
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldContain("SQL error 2601");
        record.Message.ShouldNotContain("another.superman@heroregistry.com");
    }

    private string Elapsed(int index) =>
        _logger.Collector.GetSnapshot()[index].StructuredState!.Single(kv => kv.Key == "ElapsedMs").Value!;

    private SuperHero NewHero(string heroName, string email) =>
        SuperHero.Create(Details(heroName, email), _adminId, CreatedAt);

    private static SuperHeroDetails Details(string heroName, string email) => new(
        heroName, "Natasha", "Romanoff", new DateOnly(1984, 11, 22), email,
        ["Espionage", "Martial arts"], 75, "1 Avengers Tower", null, "Manhattan", "NY", "10001");

    private sealed class HeroTestRepository(IDbConnectionFactory connections, DataAccessInstrumentation instrumentation)
        : SqlRepository<SuperHero>(connections, instrumentation, new SuperHeroMap())
    {
        public Task<IReadOnlyList<SuperHero>> FindByCityAsync(string city, CancellationToken cancellationToken) =>
            QueryAsync(
                $"{SelectFromTable} WHERE [City] = @City AND [IsDeleted] = 0",
                [Parameter(nameof(SuperHero.City), city, "@City")],
                cancellationToken);

        public async Task<(IReadOnlyList<SuperHero> Page, int Total)> FirstPageAsync(
            int pageSize, CancellationToken cancellationToken)
        {
            var total = 0;
            var page = await QueryAsync(
                $"SELECT {SelectList}, COUNT(*) OVER() AS TotalCount FROM {Metadata.QualifiedTableName} " +
                "WHERE [IsDeleted] = 0 ORDER BY [HeroName] OFFSET 0 ROWS FETCH NEXT @PageSize ROWS ONLY",
                [new SqlParameterValue("@PageSize", pageSize, System.Data.DbType.Int32, 0)],
                cancellationToken,
                row => total = row.GetInt32(row.GetOrdinal("TotalCount")));
            return (page, total);
        }

        public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
            ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {Metadata.QualifiedTableName} WHERE [IsDeleted] = 0", [], cancellationToken);
    }

    private sealed class UserTestRepository(IDbConnectionFactory connections, DataAccessInstrumentation instrumentation)
        : SqlRepository<User>(connections, instrumentation, new UserMap());
}
