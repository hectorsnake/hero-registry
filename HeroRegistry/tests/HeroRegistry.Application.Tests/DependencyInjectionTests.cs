using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Application.Heroes;
using HeroRegistry.Application.Users;
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
    public void Registers_the_use_cases_per_scope_with_the_system_clock()
    {
        // The ports are provided by Infrastructure (repositories, hasher, tokens) and the API (current user).
        using var provider = new ServiceCollection()
            .AddApplication()
            .AddScoped(_ => Substitute.For<IHeroRepository>())
            .AddScoped(_ => Substitute.For<IUserRepository>())
            .AddScoped(_ => Substitute.For<IRefreshTokenRepository>())
            .AddLogging()
            .AddSingleton(_ => Substitute.For<IPasswordHasher>())
            .AddSingleton(_ => Substitute.For<ITokenService>())
            .AddScoped(_ => Substitute.For<ICurrentUser>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IHeroService>().ShouldBeOfType<HeroService>();
        scope.ServiceProvider.GetRequiredService<IAuthService>().ShouldBeOfType<AuthService>();
        scope.ServiceProvider.GetRequiredService<IUserService>().ShouldBeOfType<UserService>();
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }
}
