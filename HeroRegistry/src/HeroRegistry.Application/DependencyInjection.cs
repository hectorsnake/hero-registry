using HeroRegistry.Application.Heroes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HeroRegistry.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer services (use cases). Their ports are provided by other layers:
    /// repositories by Infrastructure, <see cref="Abstractions.ICurrentUser"/> by the API.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Tests replace it with FakeTimeProvider.
        services.TryAddSingleton(TimeProvider.System);

        // Hero use cases (T07).
        services.AddScoped<IHeroService, HeroService>();

        // User registration and login use cases (T08) are registered here.

        return services;
    }
}
