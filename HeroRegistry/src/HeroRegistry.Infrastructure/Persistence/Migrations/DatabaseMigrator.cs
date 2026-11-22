using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Persistence.Migrations;

/// <summary>
/// A lightweight migration runner (Entity Framework migrations are not allowed in this project):
/// <list type="number">
///   <item>Creates the database when it is missing.</item>
///   <item>Records applied scripts in <c>dbo.SchemaVersions</c> (name, SHA-256 checksum, date).</item>
///   <item>Applies pending embedded scripts in order, each in its own transaction (all or nothing).</item>
///   <item>Refuses to run if an already applied script was edited.</item>
/// </list>
/// An application lock serializes concurrent runs, for example several API instances starting at once.
/// </summary>
public sealed class DatabaseMigrator(IOptions<DatabaseOptions> options, ILogger<DatabaseMigrator> logger)
    : IDatabaseMigrator
{
    private const string MigrationLockResource = "HeroRegistry.DatabaseMigrations";
    private const int LockTimeoutMilliseconds = 120_000;
    private const int ScriptTimeoutSeconds = 300;
    private const int DatabaseAlreadyExists = 1801;

    public Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default) =>
        MigrateAsync(MigrationScripts.All, cancellationToken);

    internal async Task<MigrationResult> MigrateAsync(
        IReadOnlyList<MigrationScript> scripts,
        CancellationToken cancellationToken)
    {
        var connectionString = options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{DatabaseOptions.ConnectionStringName}' is not configured.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                $"Connection string '{DatabaseOptions.ConnectionStringName}' must include a database name " +
                "(Database or Initial Catalog).");
        }

        await EnsureDatabaseExistsAsync(builder, databaseName, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await AcquireLockAsync(connection, MigrationLockResource, cancellationToken);

        try
        {
            await EnsureSchemaVersionsTableAsync(connection, cancellationToken);

            var applied = await GetAppliedScriptsAsync(connection, cancellationToken);
            EnsureAppliedScriptsAreUnchanged(scripts, applied);

            var appliedNow = new List<string>();
            foreach (var script in scripts.Where(script => !applied.ContainsKey(script.Name)))
            {
                MigrationLog.Applying(logger, script.Name);
                await ApplyAsync(connection, script, cancellationToken);
                appliedNow.Add(script.Name);
            }

            MigrationLog.UpToDate(logger, databaseName, appliedNow.Count, scripts.Count);
            return new MigrationResult(appliedNow, scripts.Count);
        }
        finally
        {
            await ReleaseLockAsync(connection, MigrationLockResource);
        }
    }

    private async Task EnsureDatabaseExistsAsync(
        SqlConnectionStringBuilder builder,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var master = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };

        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Concurrent CREATE DATABASE statements can fail on the "model" database lock, so serialize them.
        var lockResource = $"HeroRegistry.CreateDatabase.{databaseName}";
        await AcquireLockAsync(connection, lockResource, cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            // QUOTENAME makes the dynamic CREATE DATABASE safe for any database name.
            command.CommandText =
                """
                IF DB_ID(@DatabaseName) IS NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName);
                    EXEC (@sql);
                    SELECT CAST(1 AS BIT);
                END
                ELSE
                    SELECT CAST(0 AS BIT);
                """;
            command.Parameters.Add(new SqlParameter("@DatabaseName", System.Data.SqlDbType.NVarChar, 128)
            {
                Value = databaseName,
            });

            if (await command.ExecuteScalarAsync(cancellationToken) is true)
            {
                MigrationLog.DatabaseCreated(logger, databaseName);
            }
        }
        catch (SqlException ex) when (ex.Number == DatabaseAlreadyExists)
        {
            // Created by someone else in the meantime (for example Aspire's AddDatabase): nothing to do.
        }
        finally
        {
            await ReleaseLockAsync(connection, lockResource);
        }
    }

    private static async Task EnsureSchemaVersionsTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
            CREATE TABLE dbo.SchemaVersions
            (
                ScriptName NVARCHAR(255) NOT NULL CONSTRAINT PK_SchemaVersions PRIMARY KEY,
                Checksum   CHAR(64)      NOT NULL,
                AppliedAt  DATETIME2(3)  NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAt DEFAULT (SYSUTCDATETIME())
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, string>> GetAppliedScriptsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ScriptName, Checksum FROM dbo.SchemaVersions";

        var applied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied[reader.GetString(0)] = reader.GetString(1);
        }

        return applied;
    }

    private static void EnsureAppliedScriptsAreUnchanged(
        IReadOnlyList<MigrationScript> scripts,
        Dictionary<string, string> applied)
    {
        var edited = scripts
            .Where(script => applied.TryGetValue(script.Name, out var checksum)
                             && !string.Equals(checksum, script.Checksum, StringComparison.OrdinalIgnoreCase))
            .Select(script => script.Name)
            .ToList();

        if (edited.Count > 0)
        {
            throw new InvalidOperationException(
                $"Migration script(s) {string.Join(", ", edited)} changed after being applied to this database. " +
                "Applied scripts must never be edited: add a new numbered script instead.");
        }
    }

    private static async Task ApplyAsync(SqlConnection connection, MigrationScript script, CancellationToken cancellationToken)
    {
        // XACT_ABORT: any error rolls back the whole script, so a script is applied completely or not at all.
        await using (var setXactAbort = new SqlCommand("SET XACT_ABORT ON;", connection))
        {
            await setXactAbort.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var batch in SqlBatchSplitter.Split(script.Sql))
            {
                await using var command = new SqlCommand(batch, connection, transaction)
                {
                    CommandTimeout = ScriptTimeoutSeconds,
                };
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var record = new SqlCommand(
                "INSERT INTO dbo.SchemaVersions (ScriptName, Checksum) VALUES (@ScriptName, @Checksum);",
                connection,
                transaction))
            {
                record.Parameters.Add(new SqlParameter("@ScriptName", System.Data.SqlDbType.NVarChar, 255) { Value = script.Name });
                record.Parameters.Add(new SqlParameter("@Checksum", System.Data.SqlDbType.Char, 64) { Value = script.Checksum });
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // With XACT_ABORT the server may already have rolled back; the transaction then has no connection.
            if (transaction.Connection is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private static async Task AcquireLockAsync(SqlConnection connection, string resource, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DECLARE @result INT;
            EXEC @result = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive',
                                         @LockOwner = 'Session', @LockTimeout = @Timeout;
            SELECT @result;
            """;
        command.Parameters.Add(new SqlParameter("@Resource", System.Data.SqlDbType.NVarChar, 255) { Value = resource });
        command.Parameters.Add(new SqlParameter("@Timeout", System.Data.SqlDbType.Int) { Value = LockTimeoutMilliseconds });
        command.CommandTimeout = (LockTimeoutMilliseconds / 1000) + 30;

        var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        if (result < 0)
        {
            throw new TimeoutException(
                $"Could not acquire the '{resource}' lock (sp_getapplock returned {result}). " +
                "Another instance may still be migrating the database.");
        }
    }

    private static async Task ReleaseLockAsync(SqlConnection connection, string resource)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            return; // A closed connection has already released its session locks.
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = 'Session';";
        command.Parameters.Add(new SqlParameter("@Resource", System.Data.SqlDbType.NVarChar, 255) { Value = resource });
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
