using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Data.SqlClient;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Migrations;

/// <summary>
/// The schema's constraints are the last line of defense behind the Domain and Application rules.
/// Each test runs against a freshly migrated database.
/// </summary>
public sealed class SchemaConstraintsTests(SqlServerContainerFixture sqlServer) : IAsyncLifetime
{
    private const int ConstraintViolation = 547; // CHECK or FOREIGN KEY
    private const int UniqueIndexViolation = 2601;

    private readonly TestDatabase _database = new(sqlServer);
    private int _userId;

    public async ValueTask InitializeAsync()
    {
        await _database.MigrateAsync();
        // The administrator seeded by 0003_SeedData.sql.
        _userId = await _database.ScalarAsync<int>("SELECT Id FROM dbo.Users WHERE UserName = 'Admin'");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // SuperHeroes: valid data and defaults

    [Fact]
    public async Task A_valid_hero_is_stored_with_defaults()
    {
        var id = await InsertHeroAsync();

        (await _database.ScalarAsync<bool>("SELECT IsDeleted FROM dbo.SuperHeroes WHERE Id = @Id", ("@Id", id)))
            .ShouldBeFalse();
        var createdAt = await _database.ScalarAsync<DateTime>(
            "SELECT CreatedAt FROM dbo.SuperHeroes WHERE Id = @Id", ("@Id", id));
        createdAt.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    // SuperHeroes: CHECK constraints

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task PowerLevel_outside_1_to_100_is_rejected(int powerLevel)
    {
        await ShouldViolateAsync(ConstraintViolation, () => InsertHeroAsync(powerLevel: powerLevel));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task PowerLevel_boundaries_are_accepted(int powerLevel)
    {
        await InsertHeroAsync(powerLevel: powerLevel);
    }

    [Theory]
    [InlineData("Flight")]               // not JSON
    [InlineData("{\"power\":\"Flight\"}")] // JSON, but an object
    public async Task Superpowers_must_be_a_JSON_array(string superpowers)
    {
        await ShouldViolateAsync(ConstraintViolation, () => InsertHeroAsync(superpowers: superpowers));
    }

    [Theory]
    [InlineData("ny")]
    [InlineData("N1")]
    public async Task State_must_be_two_uppercase_letters(string state)
    {
        await ShouldViolateAsync(ConstraintViolation, () => InsertHeroAsync(state: state));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("12345-678")]
    [InlineData("ABCDE")]
    public async Task ZipCode_must_be_5_digits_or_ZIP_plus_4(string zipCode)
    {
        await ShouldViolateAsync(ConstraintViolation, () => InsertHeroAsync(zipCode: zipCode));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345-6789")]
    public async Task Valid_zip_codes_are_accepted(string zipCode)
    {
        await InsertHeroAsync(zipCode: zipCode);
    }

    [Fact]
    public async Task A_hero_must_be_created_by_an_existing_user()
    {
        await ShouldViolateAsync(ConstraintViolation, () => InsertHeroAsync(createdBy: 999_999));
    }

    // SuperHeroes: uniqueness among active heroes

    [Fact]
    public async Task HeroName_is_unique_among_active_heroes_ignoring_case()
    {
        await InsertHeroAsync(heroName: "Unique Hero", email: "one@heroregistry.com");

        await ShouldViolateAsync(UniqueIndexViolation,
            () => InsertHeroAsync(heroName: "unique hero", email: "two@heroregistry.com"));
    }

    [Fact]
    public async Task Hero_email_is_unique_among_active_heroes_ignoring_case()
    {
        await InsertHeroAsync(heroName: "Hero One", email: "same@heroregistry.com");

        await ShouldViolateAsync(UniqueIndexViolation,
            () => InsertHeroAsync(heroName: "Hero Two", email: "SAME@heroregistry.com"));
    }

    [Fact]
    public async Task A_soft_deleted_hero_frees_its_name_and_email()
    {
        var id = await InsertHeroAsync(heroName: "Retired Hero", email: "retired@heroregistry.com");
        await _database.ExecuteAsync("UPDATE dbo.SuperHeroes SET IsDeleted = 1 WHERE Id = @Id", ("@Id", id));

        await InsertHeroAsync(heroName: "Retired Hero", email: "retired@heroregistry.com");
    }

    // Users

    [Fact]
    public async Task A_new_user_is_not_an_admin_by_default()
    {
        var id = await InsertUserAsync("someone", "someone@heroregistry.com");

        (await _database.ScalarAsync<bool>("SELECT IsAdmin FROM dbo.Users WHERE Id = @Id", ("@Id", id))).ShouldBeFalse();
    }

    [Fact]
    public async Task UserName_is_unique_among_active_users_ignoring_case()
    {
        await ShouldViolateAsync(UniqueIndexViolation, () => InsertUserAsync("ADMIN", "other@heroregistry.com"));
    }

    [Fact]
    public async Task User_email_is_unique_among_active_users()
    {
        await ShouldViolateAsync(UniqueIndexViolation, () => InsertUserAsync("other", "Admin@HeroRegistry.com"));
    }

    [Fact]
    public async Task A_soft_deleted_user_frees_its_user_name_and_email()
    {
        var id = await InsertUserAsync("leaving", "leaving@heroregistry.com");
        await _database.ExecuteAsync("UPDATE dbo.Users SET IsDeleted = 1 WHERE Id = @Id", ("@Id", id));

        await InsertUserAsync("leaving", "leaving@heroregistry.com");
    }

    private Task<int> InsertUserAsync(string userName, string email) =>
        _database.ScalarAsync<int>(
            "INSERT INTO dbo.Users (UserName, Email, Password) OUTPUT INSERTED.Id VALUES (@UserName, @Email, @Password)",
            ("@UserName", userName), ("@Email", email), ("@Password", "$2a$11$hash-placeholder"));

    private Task<int> InsertHeroAsync(
        string? heroName = null,
        string? email = null,
        int powerLevel = 85,
        string superpowers = "[\"Super-soldier strength\",\"Shield combat\"]",
        string state = "NY",
        string zipCode = "11201",
        int? createdBy = null) =>
        _database.ScalarAsync<int>(
            """
            INSERT INTO dbo.SuperHeroes
                (HeroName, FirstName, LastName, BirthDate, Email, Superpowers, PowerLevel,
                 Address1, Address2, City, State, ZipCode, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@HeroName, 'Steve', 'Rogers', '1918-07-04', @Email, @Superpowers, @PowerLevel,
                 '569 Leaman Place', NULL, 'Brooklyn', @State, @ZipCode, @CreatedBy)
            """,
            // Unique defaults, so tests never collide with each other or with the seeded heroes.
            ("@HeroName", heroName ?? $"Test Hero {Guid.NewGuid():N}"),
            ("@Email", email ?? $"{Guid.NewGuid():N}@heroregistry.com"),
            ("@Superpowers", superpowers),
            ("@PowerLevel", powerLevel), ("@State", state), ("@ZipCode", zipCode),
            ("@CreatedBy", createdBy ?? _userId));

    private static async Task ShouldViolateAsync(int sqlErrorNumber, Func<Task> insert)
    {
        var exception = await Should.ThrowAsync<SqlException>(insert);
        exception.Number.ShouldBe(sqlErrorNumber);
    }
}
