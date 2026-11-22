using HeroRegistry.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)
                    ?? string.Empty);

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(SqlServerHealthCheck.Name);

        // Connection factory, Reflection mapper, repositories (T05, T06),
        // password hasher and token service (T08) are registered here.

        return services;
    }
}
