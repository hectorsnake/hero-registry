using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests.Fixtures;

/// <summary>
/// A uniquely named database on the shared SQL Server container, so integration tests never
/// see each other's data. The database is created by the migrator, as it would be at startup.
/// </summary>
public sealed class TestDatabase(SqlServerContainerFixture sqlServer)
{
    public string Name { get; } = $"HeroRegistryTests_{Guid.NewGuid():N}";

    public string ConnectionString { get; } = new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = string.Empty,
    }.ConnectionString;

    public string DatabaseConnectionString => new SqlConnectionStringBuilder(ConnectionString)
    {
        InitialCatalog = Name,
    }.ConnectionString;

    public string MasterConnectionString => ConnectionString;

    public DatabaseMigrator CreateMigrator() =>
        new(Options.Create(new DatabaseOptions { ConnectionString = DatabaseConnectionString }),
            NullLogger<DatabaseMigrator>.Instance);

    public Task<MigrationResult> MigrateAsync() =>
        CreateMigrator().MigrateAsync(TestContext.Current.CancellationToken);

    public async Task<T?> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = CreateCommand(connection, sql, parameters);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    public async Task<int> ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async Task<List<string>> ColumnAsync(string sql)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = CreateCommand(connection, sql, []);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var values = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    public async Task<bool> ExistsOnServerAsync()
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = CreateCommand(connection, "SELECT DB_ID(@Name)", [("@Name", Name)]);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is not (null or DBNull);
    }

    private static SqlCommand CreateCommand(
        SqlConnection connection, string sql, (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }
}
