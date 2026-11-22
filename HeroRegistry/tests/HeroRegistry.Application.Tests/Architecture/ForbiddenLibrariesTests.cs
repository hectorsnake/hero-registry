namespace HeroRegistry.Application.Tests.Architecture;

/// <summary>
/// Project constraint: the data layer is built on ADO.NET with a Reflection-based mapper,
/// so Entity Framework, Dapper and MediatR must not be used anywhere in the solution.
/// </summary>
public class ForbiddenLibrariesTests
{
    private static readonly string[] ForbiddenPackagePrefixes =
    [
        "EntityFramework",
        "Microsoft.EntityFrameworkCore",
        "Dapper",
        "MediatR",
    ];

    public static TheoryData<string> MsBuildFiles() => new(
        SolutionFiles.AllMsBuildFiles()
            .Select(path => Path.GetRelativePath(SolutionFiles.Root.FullName, path)));

    [Fact]
    public void Solution_contains_msbuild_files_to_check()
    {
        // Stops the theory below from passing with no data if the solution root is resolved wrongly.
        SolutionFiles.AllMsBuildFiles().Count().ShouldBeGreaterThanOrEqualTo(8);
    }

    [Theory]
    [MemberData(nameof(MsBuildFiles))]
    public void No_project_references_EntityFramework_Dapper_or_MediatR(string relativePath)
    {
        var fullPath = Path.Combine(SolutionFiles.Root.FullName, relativePath);

        var forbidden = SolutionFiles.PackageIds(fullPath)
            .Where(id => ForbiddenPackagePrefixes.Any(
                prefix => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        forbidden.ShouldBeEmpty($"{relativePath} references a forbidden library");
    }
}
