using HeroRegistry.Application.Common;

namespace HeroRegistry.Application.Heroes;

/// <summary>The columns heroes can be sorted by. Only these are allowed (no free-text ORDER BY).</summary>
public enum HeroSortBy
{
    HeroName,
    PowerLevel,
    CreatedAt,
}

/// <summary>Search, sort and paging options for the hero list.</summary>
/// <param name="Search">Matches hero name, first name, last name, city or a superpower (contains, ignoring case).</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page, from 1 to <see cref="MaxPageSize"/>.</param>
public sealed record HeroQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = HeroQuery.DefaultPageSize,
    HeroSortBy SortBy = HeroSortBy.HeroName,
    SortDirection SortDirection = SortDirection.Ascending)
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    /// <summary>
    /// A copy with every value in range: page at least 1, page size from 1 to the maximum, search trimmed
    /// (blank means no filter) and cut to the maximum length, and unknown sort values replaced by the defaults.
    /// </summary>
    public HeroQuery Normalize()
    {
        var search = Search?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            search = null;
        }
        else if (search.Length > MaxSearchLength)
        {
            search = search[..MaxSearchLength];
        }

        return new HeroQuery(
            search,
            Math.Max(1, Page),
            Math.Clamp(PageSize, 1, MaxPageSize),
            Enum.IsDefined(SortBy) ? SortBy : HeroSortBy.HeroName,
            Enum.IsDefined(SortDirection) ? SortDirection : SortDirection.Ascending);
    }
}
