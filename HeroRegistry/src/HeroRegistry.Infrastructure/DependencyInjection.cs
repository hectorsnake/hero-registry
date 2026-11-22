using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Repositories;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Migrations;
using HeroRegistry.Infrastructure.Persistence.Sql;
using HeroRegistry.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Infrastructure layer services (persistence, security adapters).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Configure(options =>
                options.ConnectionString =
                    configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .Validate(options => options.SlowCommandThresholdMs >= 0,
                $"'{DatabaseOptions.SlowCommandThresholdKey}' cannot be negative.")
            .Validate(options => options.MigrationMaxAttempts >= 1,
                "'Database:MigrationMaxAttempts' must be at least 1.")
            .Validate(options => options.MigrationRetryDelay >= TimeSpan.Zero,
                "'Database:MigrationRetryDelay' cannot be negative.")
            .ValidateOnStart();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(SqlServerHealthCheck.Name);

        // Data-layer timer: spans (HeroRegistry.Data) and structured logs for every DB operation (T02.2).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DataAccessInstrumentation>();

        // Schema migrations, applied when the API starts (T03).
        services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();
        services.AddHostedService<DatabaseInitializer>();

        // Reflection-based data layer (T05): connections for SqlRepository<T>.
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

        // Repositories (T06): the Application persistence ports.
        services.AddScoped<IHeroRepository, HeroRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Password hashing and access tokens (T08). Invalid JWT settings stop the application at startup.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
