using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(HeroRegistry.Api.Tests.Fixtures.SqlServerContainerFixture))]

namespace HeroRegistry.Api.Tests.Fixtures;

/// <summary>
/// One SQL Server container shared by the API tests that need a database (xUnit v3 assembly fixture).
/// It starts lazily on first use, so tests without a database don't wait for it. Docker must be running.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncDisposable
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private bool _started;

    public async Task<string> GetConnectionStringAsync()
    {
        await _startLock.WaitAsync();
        try
        {
            if (!_started)
            {
                await _container.StartAsync();
                _started = true;
            }

            return _container.GetConnectionString();
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        _startLock.Dispose();
    }
}
