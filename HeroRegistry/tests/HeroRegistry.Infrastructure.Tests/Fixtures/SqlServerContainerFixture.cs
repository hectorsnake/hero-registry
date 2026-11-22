using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(HeroRegistry.Infrastructure.Tests.Fixtures.SqlServerContainerFixture))]

namespace HeroRegistry.Infrastructure.Tests.Fixtures;

/// <summary>
/// One real SQL Server container shared by every integration test in this assembly
/// (xUnit v3 assembly fixture). Docker must be running.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    // Same engine version as Aspire (development) and Docker Compose (delivery).
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
