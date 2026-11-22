using System.Text.RegularExpressions;

namespace HeroRegistry.Infrastructure.Persistence.Migrations;

/// <summary>
/// Splits a script into batches on <c>GO</c> lines, as SSMS and sqlcmd do.
/// <c>GO</c> is a client-side separator, not T-SQL, so SQL Server would reject it.
/// </summary>
internal static partial class SqlBatchSplitter
{
    public static IReadOnlyList<string> Split(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        return GoSeparator().Split(sql)
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0)
            .ToList();
    }

    // A line containing only GO (any case), optionally surrounded by spaces or tabs.
    [GeneratedRegex(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoSeparator();
}
