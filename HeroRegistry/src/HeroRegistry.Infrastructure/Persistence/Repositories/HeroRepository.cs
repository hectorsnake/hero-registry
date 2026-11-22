using System.Data;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Heroes;
using HeroRegistry.Domain.Heroes;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Maps;
using HeroRegistry.Infrastructure.Persistence.Sql;

namespace HeroRegistry.Infrastructure.Persistence.Repositories;

/// <summary>SQL Server implementation of <see cref="IHeroRepository"/>, on the Reflection mapper (T05).</summary>
public sealed class HeroRepository(IDbConnectionFactory connections, DataAccessInstrumentation instrumentation)
    : SqlRepository<SuperHero>(connections, instrumentation, new SuperHeroMap()), IHeroRepository
{
    // LIKE patterns are built from user text, so its wildcard characters are escaped with this character.
    private const char LikeEscape = '\\';

    public Task<int> AddAsync(SuperHero hero, CancellationToken cancellationToken = default) =>
        InsertAsync(hero, cancellationToken);

    public async Task<PagedResult<SuperHero>> SearchAsync(HeroQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query = query.Normalize();

        var (filter, parameters) = BuildFilter(query.Search);
        var offset = (long)(query.Page - 1) * query.PageSize;

        var total = 0;
        var items = await QueryAsync(
            $"SELECT {SelectList}, COUNT(*) OVER() AS [TotalCount] FROM {Metadata.QualifiedTableName} " +
            $"WHERE {filter} ORDER BY {OrderBy(query)} " +
            "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            [
                .. parameters,
                new SqlParameterValue("@Offset", offset, DbType.Int64, 0),
                new SqlParameterValue("@PageSize", query.PageSize, DbType.Int32, 0),
            ],
            cancellationToken,
            row => total = row.GetInt32(row.GetOrdinal("TotalCount")));

        // A page past the end has no rows to carry COUNT(*) OVER(), so count separately.
        if (items.Count == 0 && offset > 0)
        {
            total = await ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {Metadata.QualifiedTableName} WHERE {filter}", parameters, cancellationToken);
        }

        return new PagedResult<SuperHero>(items, query.Page, query.PageSize, total);
    }

    public Task<bool> ExistsByHeroNameAsync(string heroName, int? excludeId, CancellationToken cancellationToken = default) =>
        ExistsActiveAsync(nameof(SuperHero.HeroName), heroName.Trim(), excludeId, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, int? excludeId, CancellationToken cancellationToken = default) =>
        ExistsActiveAsync(nameof(SuperHero.Email), email.Trim(), excludeId, cancellationToken);

    /// <summary>Active heroes, optionally matching the search term in any searchable column.</summary>
    private (string Sql, IReadOnlyList<SqlParameterValue> Parameters) BuildFilter(string? search)
    {
        const string active = "[IsDeleted] = 0";

        if (search is null)
        {
            return (active, []);
        }

        // Columns use a case-insensitive collation, so LIKE ignores case.
        var sql = $"{active} AND (" +
                  $"[HeroName] LIKE @Search ESCAPE '{LikeEscape}' " +
                  $"OR [FirstName] LIKE @Search ESCAPE '{LikeEscape}' " +
                  $"OR [LastName] LIKE @Search ESCAPE '{LikeEscape}' " +
                  $"OR [City] LIKE @Search ESCAPE '{LikeEscape}' " +
                  $"OR EXISTS (SELECT 1 FROM OPENJSON([Superpowers]) WHERE [value] LIKE @Search ESCAPE '{LikeEscape}'))";

        // Escaping can double the term's length, plus the two surrounding '%'.
        var pattern = $"%{EscapeLike(search)}%";
        return (sql, [new SqlParameterValue("@Search", pattern, DbType.String, (HeroQuery.MaxSearchLength * 2) + 2)]);
    }

    /// <summary>
    /// ORDER BY from a whitelist: the enum value picks one of three known columns, never user text.
    /// <c>Id</c> breaks ties, so paging is stable.
    /// </summary>
    private string OrderBy(HeroQuery query)
    {
        var column = query.SortBy switch
        {
            HeroSortBy.PowerLevel => nameof(SuperHero.PowerLevel),
            HeroSortBy.CreatedAt => nameof(SuperHero.CreatedAt),
            _ => nameof(SuperHero.HeroName),
        };
        var direction = query.SortDirection == SortDirection.Descending ? "DESC" : "ASC";

        return $"{Metadata.FindColumn(column)!.QuotedName} {direction}, {Metadata.Key.QuotedName} ASC";
    }

    /// <summary>Makes %, _ and [ match themselves instead of acting as LIKE wildcards.</summary>
    private static string EscapeLike(string value) =>
        value
            .Replace(LikeEscape.ToString(), $"{LikeEscape}{LikeEscape}", StringComparison.Ordinal)
            .Replace("%", $"{LikeEscape}%", StringComparison.Ordinal)
            .Replace("_", $"{LikeEscape}_", StringComparison.Ordinal)
            .Replace("[", $"{LikeEscape}[", StringComparison.Ordinal);
}
