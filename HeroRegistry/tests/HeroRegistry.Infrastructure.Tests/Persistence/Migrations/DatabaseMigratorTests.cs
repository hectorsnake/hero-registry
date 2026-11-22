using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Migrations;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Migrations;

/// <summary>
/// Integration tests against a real SQL Server (Testcontainers). Each test uses its own database.
/// </summary>
public class DatabaseMigratorTests(SqlServerContainerFixture sqlServer)
{
    private static readonly string[] AllScripts = ["0001_CreateUsers.sql", "0002_CreateSuperHeroes.sql", "0003_SeedData.sql"];

    private readonly TestDatabase _database = new(sqlServer);

    [Fact]
    public async Task Creates_the_database_when_it_does_not_exist()
    {
        (await _database.ExistsOnServerAsync()).ShouldBeFalse();

        await _database.MigrateAsync();

        (await _database.ExistsOnServerAsync()).ShouldBeTrue();
    }

    [Theory]
    [InlineData("dbo.Users")]
    [InlineData("dbo.SuperHeroes")]
    [InlineData("dbo.SchemaVersions")]
    public async Task Creates_the_tables(string table)
    {
        await _database.MigrateAsync();

        (await _database.ScalarAsync<int?>("SELECT OBJECT_ID(@Table, 'U')", ("@Table", table))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Applies_every_script_in_order_and_records_it_in_SchemaVersions()
    {
        var result = await _database.MigrateAsync();

        result.AppliedScripts.ShouldBe(AllScripts);
        result.TotalScripts.ShouldBe(AllScripts.Length);
        (await _database.ColumnAsync("SELECT ScriptName FROM dbo.SchemaVersions ORDER BY ScriptName"))
            .ShouldBe(AllScripts);
        (await _database.ColumnAsync("SELECT Checksum FROM dbo.SchemaVersions"))
            .ShouldAllBe(checksum => checksum.Length == 64);
    }

    [Fact]
    public async Task Running_again_is_a_no_op()
    {
        await _database.MigrateAsync();

        var second = await _database.MigrateAsync();

        second.AppliedScripts.ShouldBeEmpty();
        (await _database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions")).ShouldBe(AllScripts.Length);
    }

    [Fact]
    public async Task Concurrent_startups_apply_each_script_exactly_once()
    {
        // For example, several API instances starting at the same time.
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _database.MigrateAsync()));

        results.SelectMany(r => r.AppliedScripts).OrderBy(name => name).ShouldBe(AllScripts);
        (await _database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions")).ShouldBe(AllScripts.Length);
    }

    [Fact]
    public async Task An_edited_script_that_was_already_applied_is_rejected()
    {
        await _database.MigrateAsync();
        await _database.ExecuteAsync(
            "UPDATE dbo.SchemaVersions SET Checksum = REPLICATE('0', 64) WHERE ScriptName = @Name",
            ("@Name", "0001_CreateUsers.sql"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _database.MigrateAsync());

        exception.Message.ShouldContain("0001_CreateUsers.sql");
    }

    [Fact]
    public async Task A_failing_script_is_rolled_back_and_not_recorded()
    {
        var migrator = _database.CreateMigrator();
        MigrationScript[] scripts =
        [
            Script("0001_Good.sql", "CREATE TABLE dbo.GoodTable (Id INT);"),
            Script("0002_Bad.sql", "CREATE TABLE dbo.HalfDone (Id INT);\nGO\nSELECT 1 / 0;"),
        ];

        await Should.ThrowAsync<SqlException>(
            () => migrator.MigrateAsync(scripts, TestContext.Current.CancellationToken));

        (await _database.ScalarAsync<int?>("SELECT OBJECT_ID('dbo.GoodTable', 'U')")).ShouldNotBeNull();
        (await _database.ScalarAsync<int?>("SELECT OBJECT_ID('dbo.HalfDone', 'U')")).ShouldBeNull();
        (await _database.ColumnAsync("SELECT ScriptName FROM dbo.SchemaVersions")).ShouldBe(["0001_Good.sql"]);
    }

    [Fact]
    public async Task A_connection_string_without_a_database_name_is_rejected()
    {
        var migrator = new DatabaseMigrator(
            Options.Create(new DatabaseOptions { ConnectionString = _database.MasterConnectionString }),
            NullLogger<DatabaseMigrator>.Instance);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => migrator.MigrateAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("database name");
    }

    private static MigrationScript Script(string name, string sql) =>
        new(name, sql, MigrationScripts.ComputeChecksum(sql));
}
