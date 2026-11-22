using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;

namespace HeroRegistry.Api.Tests.Fixtures;

/// <summary>
/// The API on a fresh, uniquely named database. The API creates, migrates and seeds it at startup
/// (the Admin account and three heroes), exactly as it does under Aspire or Docker Compose.
/// </summary>
public sealed class DatabaseApiFactory : HeroRegistryApiFactory, IAsyncLifetime
{
    private readonly SqlServerContainerFixture _sqlServer;
    private string _connectionString = string.Empty;

    public DatabaseApiFactory(SqlServerContainerFixture sqlServer) => _sqlServer = sqlServer;

    public async ValueTask InitializeAsync()
    {
        _connectionString = new SqlConnectionStringBuilder(await _sqlServer.GetConnectionStringAsync())
        {
            InitialCatalog = $"HeroRegistryApiTests_{Guid.NewGuid():N}",
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:HeroRegistryDb", _connectionString);
    }
}
