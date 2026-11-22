using Microsoft.Extensions.DependencyInjection;

namespace HeroRegistry.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer services (use cases, validators).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use-case services and validators are registered here (T07, T08).

        return services;
    }
}
