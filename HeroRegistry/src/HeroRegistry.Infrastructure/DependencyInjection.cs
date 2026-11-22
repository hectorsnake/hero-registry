using System.Globalization;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            .Configure(options =>
            {
                options.ConnectionString =
                    configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty;

                if (int.TryParse(configuration[DatabaseOptions.SlowCommandThresholdKey],
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out var thresholdMs))
                {
                    options.SlowCommandThresholdMs = thresholdMs;
                }
            })
            .Validate(options => options.SlowCommandThresholdMs >= 0,
                $"'{DatabaseOptions.SlowCommandThresholdKey}' cannot be negative.");

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(SqlServerHealthCheck.Name);

        // Data-layer timer: spans (HeroRegistry.Data) and structured logs for every DB operation (T02.2).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DataAccessInstrumentation>();

        // Connection factory, Reflection mapper, repositories (T05, T06),
        // password hasher and token service (T08) are registered here.

        return services;
    }
}
