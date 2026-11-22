using System.Security.Cryptography;
using System.Text;

namespace HeroRegistry.Infrastructure.Persistence.Migrations;

/// <summary>A versioned SQL script embedded in this assembly (Persistence/Scripts/*.sql).</summary>
internal sealed record MigrationScript(string Name, string Sql, string Checksum);

/// <summary>
/// Loads the embedded migration scripts in order. Scripts are numbered (0001_, 0002_, ...),
/// so ordinal ordering by name is the execution order.
/// </summary>
internal static class MigrationScripts
{
    // Matches the LogicalName given to the scripts in HeroRegistry.Infrastructure.csproj.
    private const string ResourcePrefix = "Scripts/";

    public static IReadOnlyList<MigrationScript> All { get; } = Load();

    /// <summary>
    /// SHA-256 of the script, with line endings normalized: Git can check a script out with CRLF on
    /// Windows and LF on Linux (CI, Docker), and that must not look like an edited script.
    /// </summary>
    public static string ComputeChecksum(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var normalized = sql.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static List<MigrationScript> Load()
    {
        var assembly = typeof(MigrationScripts).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(resourceName =>
            {
                using var stream = assembly.GetManifestResourceStream(resourceName)!;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var sql = reader.ReadToEnd();
                return new MigrationScript(resourceName[ResourcePrefix.Length..], sql, ComputeChecksum(sql));
            })
            .ToList();
    }
}
