namespace HeroRegistry.Domain.Tests;

public class DomainConventionsTests
{
    /// <summary>
    /// With static instrumentation (the default on Linux, so in CI), Microsoft's code coverage rewrites
    /// the assembly under test and injects its own tracker types. They are not Domain code.
    /// </summary>
    private const string CoverageInstrumentationNamespace = "Microsoft.CodeCoverage.";

    [Fact]
    public void All_domain_types_live_in_the_Domain_namespace()
    {
        var misplaced = AssemblyReference.Assembly.GetTypes()
            .Where(t => !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .Where(t => !IsInjectedByCodeCoverage(t))
            .Where(t => t.Namespace is null || !t.Namespace.StartsWith("HeroRegistry.Domain", StringComparison.Ordinal))
            .Select(t => t.FullName);

        misplaced.ShouldBeEmpty();
    }

    private static bool IsInjectedByCodeCoverage(Type type) =>
        type.Namespace?.StartsWith(CoverageInstrumentationNamespace, StringComparison.Ordinal) == true;
}
