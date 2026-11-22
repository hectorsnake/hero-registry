using HeroRegistry.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HeroRegistry.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private const string ConnectionString = "Server=sql;Database=HeroRegistryDb;";

    [Fact]
    public void AddInfrastructure_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(ConfigurationWith(ConnectionString)).ShouldBeSameAs(services);
    }

    [Fact]
    public void AddInfrastructure_throws_when_configuration_is_null()
    {
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInfrastructure(null!));
    }

    [Fact]
    public void Binds_the_HeroRegistryDb_connection_string_to_DatabaseOptions()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(ConnectionString))
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString
            .ShouldBe(ConnectionString);
    }

    [Fact]
    public void Uses_an_empty_connection_string_when_none_is_configured()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(null))
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString
            .ShouldBeEmpty();
    }

    [Fact]
    public void Registers_the_SQL_Server_health_check()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(ConnectionString))
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Select(r => r.Name)
            .ShouldContain(SqlServerHealthCheck.Name);
    }

    private static IConfiguration ConfigurationWith(string? connectionString)
    {
        // GetConnectionString(name) reads the "ConnectionStrings" section's value for that name.
        var section = Substitute.For<IConfigurationSection>();
        section[DatabaseOptions.ConnectionStringName].Returns(connectionString);

        var configuration = Substitute.For<IConfiguration>();
        configuration.GetSection("ConnectionStrings").Returns(section);
        return configuration;
    }
}
