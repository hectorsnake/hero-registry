using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Repositories;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Migrations;
using HeroRegistry.Infrastructure.Persistence.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private const string ConnectionString = "Server=sql;Database=HeroRegistryDb;";

    [Fact]
    public void AddInfrastructure_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(Configuration()).ShouldBeSameAs(services);
    }

    [Fact]
    public void AddInfrastructure_throws_when_configuration_is_null()
    {
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInfrastructure(null!));
    }

    // Database options

    [Fact]
    public void Binds_the_HeroRegistryDb_connection_string_to_DatabaseOptions()
    {
        DatabaseOptionsFrom(Configuration()).ConnectionString.ShouldBe(ConnectionString);
    }

    [Fact]
    public void Uses_an_empty_connection_string_when_none_is_configured()
    {
        DatabaseOptionsFrom(Configuration(connectionString: null)).ConnectionString.ShouldBeEmpty();
    }

    [Fact]
    public void Uses_the_defaults_when_the_Database_section_is_missing()
    {
        var options = DatabaseOptionsFrom(Configuration());

        options.SlowCommandThresholdMs.ShouldBe(DatabaseOptions.DefaultSlowCommandThresholdMs);
        options.MigrateOnStartup.ShouldBeTrue();
        options.MigrationMaxAttempts.ShouldBe(10);
        options.MigrationRetryDelay.ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Binds_the_Database_section()
    {
        var options = DatabaseOptionsFrom(Configuration(
            ("Database:SlowCommandThresholdMs", "250"),
            ("Database:MigrateOnStartup", "false"),
            ("Database:MigrationMaxAttempts", "5"),
            ("Database:MigrationRetryDelay", "00:00:01")));

        options.SlowCommandThresholdMs.ShouldBe(250);
        options.MigrateOnStartup.ShouldBeFalse();
        options.MigrationMaxAttempts.ShouldBe(5);
        options.MigrationRetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("Database:SlowCommandThresholdMs", "-1")]
    [InlineData("Database:MigrationMaxAttempts", "0")]
    [InlineData("Database:MigrationRetryDelay", "-00:00:01")]
    public void Rejects_invalid_database_settings(string key, string value)
    {
        Should.Throw<OptionsValidationException>(() => DatabaseOptionsFrom(Configuration((key, value))));
    }

    // Registrations

    [Fact]
    public void Registers_the_SQL_Server_health_check()
    {
        using var provider = BuildProvider(Configuration());

        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Select(r => r.Name)
            .ShouldContain(SqlServerHealthCheck.Name);
    }

    [Fact]
    public void Registers_the_data_access_instrumentation_as_a_singleton()
    {
        using var provider = BuildProvider(Configuration());

        var instrumentation = provider.GetRequiredService<DataAccessInstrumentation>();

        instrumentation.ShouldBeSameAs(provider.GetRequiredService<DataAccessInstrumentation>());
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void Registers_the_migrator_and_runs_it_at_startup()
    {
        using var provider = BuildProvider(Configuration());

        provider.GetRequiredService<IDatabaseMigrator>().ShouldBeOfType<DatabaseMigrator>();
        provider.GetServices<IHostedService>().ShouldContain(service => service is DatabaseInitializer);
    }

    [Fact]
    public void Registers_the_SQL_Server_connection_factory_as_a_singleton()
    {
        using var provider = BuildProvider(Configuration());

        provider.GetRequiredService<IDbConnectionFactory>().ShouldBeOfType<SqlConnectionFactory>()
            .ShouldBeSameAs(provider.GetRequiredService<IDbConnectionFactory>());
    }

    [Fact]
    public void Registers_the_repositories_as_the_Application_ports_per_scope()
    {
        using var provider = BuildProvider(Configuration());
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IHeroRepository>().ShouldBeOfType<HeroRepository>()
            .ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<IHeroRepository>());
        scope.ServiceProvider.GetRequiredService<IUserRepository>().ShouldBeOfType<UserRepository>();
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration) =>
        new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

    private static DatabaseOptions DatabaseOptionsFrom(IConfiguration configuration)
    {
        using var provider = BuildProvider(configuration);
        return provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
        Configuration(ConnectionString, settings);

    private static IConfiguration Configuration(string? connectionString, params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings
                .Select(s => new KeyValuePair<string, string?>(s.Key, s.Value))
                .Append(new KeyValuePair<string, string?>(
                    $"ConnectionStrings:{DatabaseOptions.ConnectionStringName}", connectionString)))
            .Build();
}
