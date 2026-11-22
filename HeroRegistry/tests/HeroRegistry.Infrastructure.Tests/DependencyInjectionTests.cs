using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HeroRegistry.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private static readonly IConfiguration EmptyConfiguration = Substitute.For<IConfiguration>();

    [Fact]
    public void AddInfrastructure_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(EmptyConfiguration).ShouldBeSameAs(services);
    }

    [Fact]
    public void AddInfrastructure_throws_when_configuration_is_null()
    {
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInfrastructure(null!));
    }
}
