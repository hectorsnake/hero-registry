using HeroRegistry.Application.Common;
using HeroRegistry.Application.Heroes;
using SortDirection = HeroRegistry.Application.Common.SortDirection;
using HeroRegistry.Infrastructure.Tests.Fixtures;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Repositories;

/// <summary>HeroRepository against a real SQL Server (Testcontainers), starting from the seed data.</summary>
public class HeroRepositoryTests(SqlServerContainerFixture sqlServer) : RepositoryTestBase(sqlServer)
{
    private static readonly string[] Seeded = ["Captain America", "Spider-Man", "Superman"];

    // CRUD round-trip

    [Fact]
    public async Task Add_get_update_and_soft_delete_round_trip()
    {
        var hero = NewHero("Black Widow", powerLevel: 75);

        var id = await Heroes.AddAsync(hero, Ct);
        (await Heroes.GetByIdAsync(id, Ct))!.HeroName.ShouldBe("Black Widow");

        hero.Update(Details(hero) with { PowerLevel = 80 }, AdminId, Now.AddHours(1));
        (await Heroes.UpdateAsync(hero, Ct)).ShouldBeTrue();
        (await Heroes.GetByIdAsync(id, Ct))!.PowerLevel.ShouldBe(80);

        hero.SoftDelete(AdminId, Now.AddHours(2));
        (await Heroes.SoftDeleteAsync(hero, Ct)).ShouldBeTrue();
        (await Heroes.GetByIdAsync(id, Ct)).ShouldBeNull();
    }

    // Search

    [Fact]
    public async Task Without_a_search_term_every_active_hero_is_returned_sorted_by_name()
    {
        var result = await Heroes.SearchAsync(new HeroQuery(), Ct);

        result.Items.Select(h => h.HeroName).ShouldBe(Seeded);
        result.TotalCount.ShouldBe(3);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(HeroQuery.DefaultPageSize);
    }

    [Theory]
    [InlineData("america", "Captain America")] // hero name, ignoring case
    [InlineData("Peter", "Spider-Man")]        // first name
    [InlineData("kent", "Superman")]           // last name
    [InlineData("Queens", "Spider-Man")]       // city
    [InlineData("heat vision", "Superman")]    // a superpower (OPENJSON)
    [InlineData("WEB-SLING", "Spider-Man")]    // part of a superpower, ignoring case
    [InlineData("  shield  ", "Captain America")] // trimmed
    public async Task Search_matches_name_first_name_last_name_city_or_a_superpower(string search, string expected)
    {
        var result = await Heroes.SearchAsync(new HeroQuery(Search: search), Ct);

        result.Items.Select(h => h.HeroName).ShouldBe([expected]);
        result.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_search_term_can_match_several_heroes()
    {
        var result = await Heroes.SearchAsync(new HeroQuery(Search: "Super"), Ct);

        // "Superman" (name), "Super-soldier strength" (Captain America), "Superhuman strength" (Spider-Man)
        result.Items.Select(h => h.HeroName).ShouldBe(Seeded);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task A_search_without_matches_returns_an_empty_page()
    {
        var result = await Heroes.SearchAsync(new HeroQuery(Search: "Batmobile"), Ct);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("%", "100% Hero")]
    [InlineData("_", "Under_Score")]
    [InlineData("[", "Bracket [Man]")]
    public async Task LIKE_wildcards_in_the_search_term_are_matched_literally(string search, string expected)
    {
        await Heroes.AddAsync(NewHero("100% Hero"), Ct);
        await Heroes.AddAsync(NewHero("Under_Score"), Ct);
        await Heroes.AddAsync(NewHero("Bracket [Man]"), Ct);

        var result = await Heroes.SearchAsync(new HeroQuery(Search: search), Ct);

        result.Items.Select(h => h.HeroName).ShouldBe([expected]);
    }

    [Fact]
    public async Task Search_terms_are_data_not_SQL()
    {
        var result = await Heroes.SearchAsync(new HeroQuery(Search: "' OR 1=1; DROP TABLE dbo.SuperHeroes;--"), Ct);

        result.Items.ShouldBeEmpty();
        (await Heroes.SearchAsync(new HeroQuery(), Ct)).TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task Deleted_heroes_are_never_returned_or_counted()
    {
        var superman = (await Heroes.GetByIdAsync(await SeededHeroIdAsync("Superman"), Ct))!;
        superman.SoftDelete(AdminId, Now);
        await Heroes.SoftDeleteAsync(superman, Ct);

        var all = await Heroes.SearchAsync(new HeroQuery(), Ct);
        var flight = await Heroes.SearchAsync(new HeroQuery(Search: "Flight"), Ct);

        all.Items.Select(h => h.HeroName).ShouldBe(["Captain America", "Spider-Man"]);
        all.TotalCount.ShouldBe(2);
        flight.TotalCount.ShouldBe(0);
    }

    // Paging

    [Fact]
    public async Task Pages_contain_page_size_items_and_the_total_of_all_matches()
    {
        var first = await Heroes.SearchAsync(new HeroQuery(Page: 1, PageSize: 2), Ct);
        var second = await Heroes.SearchAsync(new HeroQuery(Page: 2, PageSize: 2), Ct);

        first.Items.Select(h => h.HeroName).ShouldBe(["Captain America", "Spider-Man"]);
        second.Items.Select(h => h.HeroName).ShouldBe(["Superman"]);
        first.TotalCount.ShouldBe(3);
        second.TotalCount.ShouldBe(3);
        first.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_still_reports_the_total()
    {
        var result = await Heroes.SearchAsync(new HeroQuery(Page: 99, PageSize: 2), Ct);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(3);
        result.Page.ShouldBe(99);
    }

    [Fact]
    public async Task Out_of_range_paging_values_are_normalized()
    {
        var result = await Heroes.SearchAsync(new HeroQuery(Page: -3, PageSize: 5000), Ct);

        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(HeroQuery.MaxPageSize);
        result.Items.Count.ShouldBe(3);
    }

    // Sorting (whitelisted)

    [Theory]
    [InlineData(HeroSortBy.PowerLevel, SortDirection.Descending, new[] { "Superman", "Captain America", "Spider-Man" })]
    [InlineData(HeroSortBy.PowerLevel, SortDirection.Ascending, new[] { "Spider-Man", "Captain America", "Superman" })]
    [InlineData(HeroSortBy.HeroName, SortDirection.Descending, new[] { "Superman", "Spider-Man", "Captain America" })]
    public async Task Results_can_be_sorted_by_a_whitelisted_column_and_direction(
        HeroSortBy sortBy, SortDirection direction, string[] expected)
    {
        var result = await Heroes.SearchAsync(new HeroQuery(SortBy: sortBy, SortDirection: direction), Ct);

        result.Items.Select(h => h.HeroName).ShouldBe(expected);
    }

    [Fact]
    public async Task Sorting_by_CreatedAt_descending_lists_the_newest_hero_first()
    {
        await Heroes.AddAsync(NewHero("Newcomer"), Ct);

        var result = await Heroes.SearchAsync(
            new HeroQuery(SortBy: HeroSortBy.CreatedAt, SortDirection: SortDirection.Descending), Ct);

        result.Items[0].HeroName.ShouldBe("Newcomer");
    }

    [Fact]
    public async Task An_unknown_sort_value_falls_back_to_hero_name_ascending()
    {
        var result = await Heroes.SearchAsync(new HeroQuery(SortBy: (HeroSortBy)99, SortDirection: (SortDirection)7), Ct);

        result.Items.Select(h => h.HeroName).ShouldBe(Seeded);
    }

    // Uniqueness checks

    [Theory]
    [InlineData("Superman")]
    [InlineData("SUPERMAN")]
    [InlineData("  superman ")]
    public async Task ExistsByHeroName_ignores_case_and_surrounding_spaces(string heroName)
    {
        (await Heroes.ExistsByHeroNameAsync(heroName, excludeId: null, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByHeroName_ignores_the_excluded_hero_so_it_can_keep_its_own_name()
    {
        var supermanId = await SeededHeroIdAsync("Superman");

        (await Heroes.ExistsByHeroNameAsync("Superman", excludeId: supermanId, Ct)).ShouldBeFalse();
        (await Heroes.ExistsByHeroNameAsync("Spider-Man", excludeId: supermanId, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Exists_checks_ignore_deleted_heroes()
    {
        var superman = (await Heroes.GetByIdAsync(await SeededHeroIdAsync("Superman"), Ct))!;
        superman.SoftDelete(AdminId, Now);
        await Heroes.SoftDeleteAsync(superman, Ct);

        (await Heroes.ExistsByHeroNameAsync("Superman", null, Ct)).ShouldBeFalse();
        (await Heroes.ExistsByEmailAsync("superman@heroregistry.com", null, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByEmail_finds_an_active_hero_email_ignoring_case()
    {
        (await Heroes.ExistsByEmailAsync("Spider-Man@HeroRegistry.com", null, Ct)).ShouldBeTrue();
        (await Heroes.ExistsByEmailAsync("nobody@heroregistry.com", null, Ct)).ShouldBeFalse();
        (await Heroes.ExistsByEmailAsync("spider-man@heroregistry.com", await SeededHeroIdAsync("Spider-Man"), Ct))
            .ShouldBeFalse();
    }

    // Unique violations (safety net for race conditions)

    [Fact]
    public async Task Adding_a_duplicate_hero_name_throws_DuplicateEntityException_naming_the_field()
    {
        var exception = await Should.ThrowAsync<DuplicateEntityException>(
            () => Heroes.AddAsync(NewHero("superman", email: "secret.identity@heroregistry.com"), Ct));

        exception.EntityName.ShouldBe("SuperHero");
        exception.Field.ShouldBe("HeroName");
        exception.Message.ShouldNotContain("superman");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public async Task Adding_a_duplicate_email_names_the_Email_field_without_the_address()
    {
        var exception = await Should.ThrowAsync<DuplicateEntityException>(
            () => Heroes.AddAsync(NewHero("Imposter", email: "superman@heroregistry.com"), Ct));

        exception.Field.ShouldBe("Email");
        exception.Message.ShouldNotContain("superman@heroregistry.com");
    }

    [Fact]
    public async Task Renaming_a_hero_to_an_existing_name_throws_DuplicateEntityException()
    {
        var hero = NewHero("Renamed Later");
        await Heroes.AddAsync(hero, Ct);
        hero.Update(Details(hero) with { HeroName = "Spider-Man" }, AdminId, Now.AddHours(1));

        (await Should.ThrowAsync<DuplicateEntityException>(() => Heroes.UpdateAsync(hero, Ct))).Field.ShouldBe("HeroName");
    }

    private static Domain.Heroes.SuperHeroDetails Details(Domain.Heroes.SuperHero hero) => new(
        hero.HeroName, hero.FirstName, hero.LastName, hero.BirthDate, hero.Email, hero.Superpowers,
        hero.PowerLevel, hero.Address1, hero.Address2, hero.City, hero.State, hero.ZipCode);
}
