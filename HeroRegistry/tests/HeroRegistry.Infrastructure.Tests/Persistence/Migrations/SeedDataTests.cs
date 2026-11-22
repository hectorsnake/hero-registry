using HeroRegistry.Infrastructure.Persistence.Migrations;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Data.SqlClient;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Migrations;

/// <summary>
/// T03.1: 0003_SeedData.sql creates the demo administrator and three heroes.
/// Each test runs against a freshly migrated database.
/// </summary>
public sealed class SeedDataTests(SqlServerContainerFixture sqlServer) : IAsyncLifetime
{
    private readonly TestDatabase _database = new(sqlServer);

    public async ValueTask InitializeAsync() => await _database.MigrateAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Administrator

    [Fact]
    public async Task Seeds_exactly_one_user_the_administrator()
    {
        (await _database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users")).ShouldBe(1);
        (await _database.ScalarAsync<string>("SELECT Email FROM dbo.Users WHERE UserName = 'Admin'"))
            .ShouldBe("admin@heroregistry.com");
        (await _database.ScalarAsync<bool>("SELECT IsAdmin FROM dbo.Users WHERE UserName = 'Admin'"))
            .ShouldBeTrue();
        (await _database.ScalarAsync<bool>("SELECT IsDeleted FROM dbo.Users WHERE UserName = 'Admin'"))
            .ShouldBeFalse();
        (await _database.ScalarAsync<int?>("SELECT CreatedBy FROM dbo.Users WHERE UserName = 'Admin'"))
            .ShouldBeNull(); // created by the system
    }

    [Fact]
    public async Task The_administrator_password_is_stored_as_a_BCrypt_hash_of_Admin()
    {
        var stored = await _database.ScalarAsync<string>("SELECT Password FROM dbo.Users WHERE UserName = 'Admin'");

        stored.ShouldNotBe("Admin");
        stored.ShouldStartWith("$2a$11$"); // BCrypt, work factor 11
        BCrypt.Net.BCrypt.Verify("Admin", stored).ShouldBeTrue();
        BCrypt.Net.BCrypt.Verify("admin", stored).ShouldBeFalse();
    }

    // Heroes

    [Fact]
    public async Task Seeds_Captain_America_Spider_Man_and_Superman()
    {
        (await _database.ColumnAsync("SELECT HeroName FROM dbo.SuperHeroes ORDER BY HeroName"))
            .ShouldBe(["Captain America", "Spider-Man", "Superman"]);
    }

    [Fact]
    public async Task Every_seeded_hero_is_active_and_created_by_the_administrator()
    {
        var adminId = await _database.ScalarAsync<int>("SELECT Id FROM dbo.Users WHERE UserName = 'Admin'");

        (await _database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.SuperHeroes WHERE CreatedBy = @AdminId AND IsDeleted = 0 AND UpdatedBy IS NULL",
                ("@AdminId", adminId)))
            .ShouldBe(3);
    }

    [Theory]
    [InlineData("Captain America", "Steve", "Rogers", "1918-07-04", "captain.america@heroregistry.com", 85, "569 Leaman Place", null, "Brooklyn", "NY", "11201")]
    [InlineData("Spider-Man", "Peter", "Parker", "2001-08-10", "spider-man@heroregistry.com", 80, "20 Ingram Street", null, "Queens", "NY", "11375")]
    [InlineData("Superman", "Clark", "Kent", "1977-06-18", "superman@heroregistry.com", 100, "344 Clinton Street", "Apt 3D", "Metropolis", "NY", "10001")]
    public async Task Seeded_heroes_match_the_T03_1_table(
        string heroName, string firstName, string lastName, string birthDate, string email, int powerLevel,
        string address1, string? address2, string city, string state, string zipCode)
    {
        const string sql =
            """
            SELECT CONCAT_WS('|', FirstName, LastName, CONVERT(CHAR(10), BirthDate, 23), Email, PowerLevel,
                             Address1, ISNULL(Address2, '<null>'), City, State, ZipCode)
            FROM dbo.SuperHeroes WHERE HeroName = @HeroName
            """;

        (await _database.ScalarAsync<string>(sql, ("@HeroName", heroName)))
            .ShouldBe(string.Join('|', firstName, lastName, birthDate, email, powerLevel,
                address1, address2 ?? "<null>", city, state, zipCode));
    }

    [Theory]
    [InlineData("Captain America", new[] { "Super-soldier strength", "Enhanced agility", "Shield combat", "Tactical leadership" })]
    [InlineData("Spider-Man", new[] { "Wall-crawling", "Spider-sense", "Superhuman strength", "Web-slinging" })]
    [InlineData("Superman", new[] { "Flight", "Super strength", "Heat vision", "Invulnerability", "X-ray vision" })]
    public async Task Seeded_superpowers_are_a_JSON_array_of_names(string heroName, string[] expected)
    {
        var powers = await _database.ColumnAsync(
            $"SELECT value FROM dbo.SuperHeroes CROSS APPLY OPENJSON(Superpowers) WHERE HeroName = N'{heroName}' ORDER BY CAST([key] AS INT)");

        powers.ShouldBe(expected);
    }

    // Idempotency

    [Fact]
    public async Task Running_the_seed_script_again_does_not_duplicate_rows()
    {
        var seed = MigrationScripts.All.Single(s => s.Name == "0003_SeedData.sql");

        await using (var connection = new SqlConnection(_database.DatabaseConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            foreach (var batch in SqlBatchSplitter.Split(seed.Sql))
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
        }

        (await _database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users")).ShouldBe(1);
        (await _database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SuperHeroes")).ShouldBe(3);
    }
}
