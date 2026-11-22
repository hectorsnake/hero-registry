using System.Reflection;

namespace HeroRegistry.Application.Tests.Architecture;

/// <summary>
/// Clean Architecture dependency rule: dependencies point inward
/// (Api and Infrastructure, then Application, then Domain), and Domain depends on nothing.
/// Each rule is checked on the declared project files and on the compiled assemblies.
/// </summary>
public class LayerDependencyTests
{
    private const string DomainProject = "HeroRegistry.Domain";
    private const string ApplicationProject = "HeroRegistry.Application";
    private const string InfrastructureProject = "HeroRegistry.Infrastructure";
    private const string ApiProject = "HeroRegistry.Api";

    private static readonly Assembly DomainAssembly = Domain.AssemblyReference.Assembly;
    private static readonly Assembly ApplicationAssembly = AssemblyReference.Assembly;

    // Domain

    [Fact]
    public void Domain_project_declares_no_project_references()
    {
        SolutionFiles.ProjectReferences(SolutionFiles.SourceProject(DomainProject)).ShouldBeEmpty();
    }

    [Fact]
    public void Domain_project_declares_no_package_references()
    {
        SolutionFiles.PackageIds(SolutionFiles.SourceProject(DomainProject)).ShouldBeEmpty();
    }

    [Fact]
    public void Domain_assembly_references_only_the_base_class_library()
    {
        var nonFrameworkReferences = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !IsBaseClassLibrary(name));

        nonFrameworkReferences.ShouldBeEmpty();
    }

    // Application

    [Fact]
    public void Application_project_references_only_Domain()
    {
        SolutionFiles.ProjectReferences(SolutionFiles.SourceProject(ApplicationProject))
            .ShouldBe([DomainProject]);
    }

    [Theory]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("System.Data.SqlClient")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_project_declares_no_data_provider_or_web_packages(string forbiddenPrefix)
    {
        SolutionFiles.PackageIds(SolutionFiles.SourceProject(ApplicationProject))
            .ShouldNotContain(id => id.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(InfrastructureProject)]
    [InlineData(ApiProject)]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_assembly_does_not_reference(string forbiddenPrefix)
    {
        ApplicationAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ShouldNotContain(name => name.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase));
    }

    // Infrastructure

    [Fact]
    public void Infrastructure_project_does_not_reference_Api()
    {
        SolutionFiles.ProjectReferences(SolutionFiles.SourceProject(InfrastructureProject))
            .ShouldNotContain(ApiProject);
    }

    private static bool IsBaseClassLibrary(string assemblyName) =>
        assemblyName is "netstandard" or "mscorlib" or "System"
        || assemblyName.StartsWith("System.", StringComparison.Ordinal);
}
