using HeroRegistry.Application.Common;
using HeroRegistry.Application.Heroes;
using SortDirection = HeroRegistry.Application.Common.SortDirection;

namespace HeroRegistry.Application.Tests.Common;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(3, 2, 2)]
    public void TotalPages_rounds_up(int totalCount, int pageSize, int expectedPages)
    {
        new PagedResult<string>([], Page: 1, pageSize, totalCount).TotalPages.ShouldBe(expectedPages);
    }

    [Fact]
    public void HasNextPage_and_HasPreviousPage_follow_the_page_number()
    {
        var middle = new PagedResult<string>(["b"], Page: 2, PageSize: 1, TotalCount: 3);

        middle.HasPreviousPage.ShouldBeTrue();
        middle.HasNextPage.ShouldBeTrue();
        (middle with { Page = 3 }).HasNextPage.ShouldBeFalse();
        (middle with { Page = 1 }).HasPreviousPage.ShouldBeFalse();
    }
}

public class HeroQueryTests
{
    [Fact]
    public void Defaults_to_the_first_page_of_10_sorted_by_hero_name()
    {
        var query = new HeroQuery();

        query.Search.ShouldBeNull();
        query.Page.ShouldBe(1);
        query.PageSize.ShouldBe(HeroQuery.DefaultPageSize);
        query.SortBy.ShouldBe(HeroSortBy.HeroName);
        query.SortDirection.ShouldBe(SortDirection.Ascending);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Normalized_page_is_at_least_1(int page, int expected)
    {
        new HeroQuery(Page: page).Normalize().Page.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(25, 25)]
    [InlineData(1000, HeroQuery.MaxPageSize)]
    public void Normalized_page_size_is_between_1_and_the_maximum(int pageSize, int expected)
    {
        new HeroQuery(PageSize: pageSize).Normalize().PageSize.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  flight ", "flight")]
    public void Normalized_search_is_trimmed_and_blank_means_no_filter(string? search, string? expected)
    {
        new HeroQuery(Search: search).Normalize().Search.ShouldBe(expected);
    }

    [Fact]
    public void Normalized_search_is_cut_to_the_maximum_length()
    {
        new HeroQuery(Search: new string('a', 500)).Normalize().Search!.Length.ShouldBe(HeroQuery.MaxSearchLength);
    }

    [Fact]
    public void Unknown_sort_values_fall_back_to_the_defaults()
    {
        var query = new HeroQuery(SortBy: (HeroSortBy)99, SortDirection: (SortDirection)42).Normalize();

        query.SortBy.ShouldBe(HeroSortBy.HeroName);
        query.SortDirection.ShouldBe(SortDirection.Ascending);
    }
}

public class DuplicateEntityExceptionTests
{
    [Fact]
    public void Names_the_entity_and_field_without_the_duplicate_value()
    {
        var exception = new DuplicateEntityException("SuperHero", "HeroName");

        exception.EntityName.ShouldBe("SuperHero");
        exception.Field.ShouldBe("HeroName");
        exception.Message.ShouldBe("A SuperHero with the same HeroName already exists.");
        exception.InnerException.ShouldBeNull(); // the SQL error text contains the duplicate value
    }

    [Fact]
    public void Works_when_the_field_is_unknown()
    {
        var exception = new DuplicateEntityException("User", field: null);

        exception.Message.ShouldBe("A User with the same unique value already exists.");
    }
}
