using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Persistence.Sql;

/// <summary>
/// Opens database connections. The data layer works with the ADO.NET base classes (DbConnection,
/// DbCommand, DbParameter), so only this factory knows the provider.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>SQL Server connections (Microsoft.Data.SqlClient, with its connection pool).</summary>
public sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
{
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{DatabaseOptions.ConnectionStringName}' is not configured.");
        }

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
