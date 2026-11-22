using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
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

    [Fact]
    public void Registers_the_data_access_instrumentation_as_a_singleton()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(ConfigurationWith(ConnectionString))
            .BuildServiceProvider();

        var instrumentation = provider.GetRequiredService<DataAccessInstrumentation>();

        instrumentation.ShouldBeSameAs(provider.GetRequiredService<DataAccessInstrumentation>());
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void Uses_the_default_slow_command_threshold_when_none_is_configured()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(ConnectionString))
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.SlowCommandThresholdMs
            .ShouldBe(DatabaseOptions.DefaultSlowCommandThresholdMs);
    }

    [Fact]
    public void Binds_the_slow_command_threshold()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(ConnectionString, slowThresholdMs: "250"))
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.SlowCommandThresholdMs.ShouldBe(250);
    }

    [Fact]
    public void Rejects_a_negative_slow_command_threshold()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(ConnectionString, slowThresholdMs: "-1"))
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);
    }

    private static IConfiguration ConfigurationWith(string? connectionString, string? slowThresholdMs = null)
    {
        // GetConnectionString(name) reads the "ConnectionStrings" section's value for that name.
        var section = Substitute.For<IConfigurationSection>();
        section[DatabaseOptions.ConnectionStringName].Returns(connectionString);

        var configuration = Substitute.For<IConfiguration>();
        configuration.GetSection("ConnectionStrings").Returns(section);
        configuration[DatabaseOptions.SlowCommandThresholdKey].Returns(slowThresholdMs);
        return configuration;
    }
}
