using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Sql;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests.Persistence;

public class SqlConnectionFactoryTests(SqlServerContainerFixture sqlServer)
{
    [Fact]
    public async Task Opens_a_connection_to_the_configured_database()
    {
        var factory = new SqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = sqlServer.ConnectionString }));

        await using var connection = await factory.OpenConnectionAsync(TestContext.Current.CancellationToken);

        connection.State.ShouldBe(System.Data.ConnectionState.Open);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Fails_clearly_when_no_connection_string_is_configured(string connectionString)
    {
        var factory = new SqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString }));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => factory.OpenConnectionAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain(DatabaseOptions.ConnectionStringName);
    }
}
