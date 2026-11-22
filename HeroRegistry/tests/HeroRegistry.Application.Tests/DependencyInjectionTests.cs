using Microsoft.Extensions.DependencyInjection;

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
}
