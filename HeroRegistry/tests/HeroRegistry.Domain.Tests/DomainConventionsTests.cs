namespace HeroRegistry.Domain.Tests;

public class DomainConventionsTests
{
    [Fact]
    public void All_domain_types_live_in_the_Domain_namespace()
    {
        var misplaced = AssemblyReference.Assembly.GetTypes()
            .Where(t => !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .Where(t => t.Namespace is null || !t.Namespace.StartsWith("HeroRegistry.Domain", StringComparison.Ordinal))
            .Select(t => t.FullName);

        misplaced.ShouldBeEmpty();
    }
}
