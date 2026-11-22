using HeroRegistry.Infrastructure.Persistence.Migrations;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Migrations;

public class MigrationScriptsTests
{
    [Fact]
    public void Embedded_scripts_are_loaded_in_numeric_order()
    {
        MigrationScripts.All.Select(s => s.Name)
            .ShouldBe(["0001_CreateUsers.sql", "0002_CreateSuperHeroes.sql", "0003_SeedData.sql"]);
    }

    [Fact]
    public void Each_script_has_content_and_a_SHA256_checksum()
    {
        MigrationScripts.All.ShouldAllBe(s =>
            !string.IsNullOrWhiteSpace(s.Sql) && s.Checksum.Length == 64);
    }

    [Fact]
    public void Checksum_ignores_line_ending_differences_between_Windows_and_Linux()
    {
        // Git may check scripts out with CRLF on Windows and LF on Linux (CI, Docker builds).
        MigrationScripts.ComputeChecksum("CREATE TABLE A (Id INT);\r\nGO\r\n")
            .ShouldBe(MigrationScripts.ComputeChecksum("CREATE TABLE A (Id INT);\nGO\n"));
    }

    [Fact]
    public void Checksum_changes_when_the_script_changes()
    {
        MigrationScripts.ComputeChecksum("CREATE TABLE A (Id INT);")
            .ShouldNotBe(MigrationScripts.ComputeChecksum("CREATE TABLE A (Id BIGINT);"));
    }
}

public class SqlBatchSplitterTests
{
    [Fact]
    public void Splits_on_GO_lines()
    {
        SqlBatchSplitter.Split("CREATE TABLE A (Id INT);\nGO\nCREATE INDEX IX ON A (Id);\nGO\n")
            .ShouldBe(["CREATE TABLE A (Id INT);", "CREATE INDEX IX ON A (Id);"]);
    }

    [Theory]
    [InlineData("SELECT 1\r\ngo\r\nSELECT 2")]
    [InlineData("SELECT 1\n  GO  \nSELECT 2")]
    [InlineData("SELECT 1\nGo\nSELECT 2")]
    public void GO_separator_is_case_insensitive_and_may_have_surrounding_spaces(string sql)
    {
        SqlBatchSplitter.Split(sql).ShouldBe(["SELECT 1", "SELECT 2"]);
    }

    [Fact]
    public void GO_inside_a_line_or_word_is_not_a_separator()
    {
        const string sql = "SELECT 'GO' AS Word, 1 AS GOAL;\nGOTO Done;";

        SqlBatchSplitter.Split(sql).ShouldBe([sql]);
    }

    [Fact]
    public void Empty_batches_are_skipped()
    {
        SqlBatchSplitter.Split("GO\n\nSELECT 1\nGO\nGO\n   \n").ShouldBe(["SELECT 1"]);
    }
}
