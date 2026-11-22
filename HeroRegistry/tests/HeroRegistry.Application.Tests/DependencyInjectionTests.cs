using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Heroes;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HeroRegistry.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddApplication().ShouldBeSameAs(services);
    }

    [Fact]
    public void AddApplication_throws_when_services_is_null()
    {
        Should.Throw<ArgumentNullException>(() => DependencyInjection.AddApplication(null!));
    }

    [Fact]
    public void Registers_the_hero_use_cases_per_scope_with_the_system_clock()
    {
        // The ports are provided by Infrastructure (repository) and the API (current user).
        using var provider = new ServiceCollection()
            .AddApplication()
            .AddScoped(_ => Substitute.For<IHeroRepository>())
            .AddScoped(_ => Substitute.For<ICurrentUser>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IHeroService>().ShouldBeOfType<HeroService>();
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }
}
