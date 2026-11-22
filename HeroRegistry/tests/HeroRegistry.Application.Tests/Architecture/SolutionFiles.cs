using System.Xml.Linq;

namespace HeroRegistry.Application.Tests.Architecture;

/// <summary>
/// Locates the solution folder and reads its MSBuild files, so tests can check the references
/// that are <em>declared</em>. The compiler drops unused assembly references, so checking only
/// compiled assemblies would miss a reference that is declared but not yet used.
/// </summary>
internal static class SolutionFiles
{
    private const string SolutionFileName = "HeroRegistry.slnx";

    public static DirectoryInfo Root { get; } = FindRoot();

    public static string SourceProject(string name) =>
        Path.Combine(Root.FullName, "src", name, $"{name}.csproj");

    /// <summary>Every .csproj and .props file in the solution, excluding bin and obj.</summary>
    public static IEnumerable<string> AllMsBuildFiles() =>
        Root.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(f => f.Extension is ".csproj" or ".props")
            .Where(f => !IsBuildOutput(f.FullName))
            .Select(f => f.FullName);

    /// <summary>Names of the projects referenced through ProjectReference items.</summary>
    public static IReadOnlyList<string> ProjectReferences(string msBuildFile) =>
        ItemIncludes(msBuildFile, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToList();

    /// <summary>Package ids from PackageReference and PackageVersion items.</summary>
    public static IReadOnlyList<string> PackageIds(string msBuildFile) =>
        ItemIncludes(msBuildFile, "PackageReference")
            .Concat(ItemIncludes(msBuildFile, "PackageVersion"))
            .ToList();

    private static IEnumerable<string> ItemIncludes(string msBuildFile, string itemName) =>
        XDocument.Load(msBuildFile)
            .Descendants()
            .Where(e => e.Name.LocalName == itemName)
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>();

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase);
    }

    private static DirectoryInfo FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return dir;
            }
        }

        throw new InvalidOperationException(
            $"Could not find '{SolutionFileName}' in any folder above '{AppContext.BaseDirectory}'.");
    }
}
