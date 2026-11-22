using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HeroRegistry.Api.Tests.Fixtures;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Heroes;
using static HeroRegistry.Api.Tests.Fixtures.ApiClientExtensions;

namespace HeroRegistry.Api.Tests.Heroes;

/// <summary>
/// /api/heroes end to end (AC3 to AC8): real SQL Server, migrated and seeded by the API with
/// Captain America, Spider-Man and Superman. Tests share the class's database, so every hero they
/// create has a unique name.
/// </summary>
public class HeroesEndpointsTests(DatabaseApiFactory factory) : IClassFixture<DatabaseApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // AC3: every hero endpoint requires a token

    [Theory]
    [InlineData("GET", "/api/heroes")]
    [InlineData("GET", "/api/heroes/1")]
    [InlineData("POST", "/api/heroes")]
    [InlineData("PUT", "/api/heroes/1")]
    [InlineData("DELETE", "/api/heroes/1")]
    public async Task Every_hero_endpoint_returns_401_without_a_token(string method, string url)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(NewHero("Anonymous"));
        }

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // AC5: list and search (seed data)

    [Fact]
    public async Task The_seeded_heroes_are_listed_and_searchable_by_superpower()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        var result = await client.GetFromJsonAsync<PagedResult<HeroResponse>>("/api/heroes?search=Flight", Ct);

        var superman = result!.Items.ShouldHaveSingleItem();
        superman.HeroName.ShouldBe("Superman");
        superman.Superpowers.ShouldContain("Flight");
        superman.BirthDate.ShouldBe(new DateOnly(1977, 6, 18));
        result.TotalCount.ShouldBe(1);
        client.Dispose();
    }

    [Fact]
    public async Task Dates_are_sent_as_yyyy_MM_dd_and_superpowers_as_a_JSON_array()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        using var response = await client.GetAsync("/api/heroes?search=Superman", Ct);
        var hero = (await response.ReadJsonAsync()).GetProperty("items")[0];

        hero.GetProperty("birthDate").GetString().ShouldBe("1977-06-18");
        hero.GetProperty("superpowers").ValueKind.ShouldBe(JsonValueKind.Array);
        hero.GetProperty("heroName").GetString().ShouldBe("Superman");
        client.Dispose();
    }

    [Fact]
    public async Task Without_query_string_the_first_page_of_10_sorted_by_name_is_returned()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        var result = await client.GetFromJsonAsync<PagedResult<HeroResponse>>("/api/heroes", Ct);

        result!.Page.ShouldBe(1);
        result.PageSize.ShouldBe(10);
        result.Items.Select(h => h.HeroName).ShouldBe(result.Items.Select(h => h.HeroName).Order(StringComparer.OrdinalIgnoreCase));
        client.Dispose();
    }

    [Fact]
    public async Task Paging_search_and_sorting_are_bound_from_the_query_string()
    {
        var (_, client) = await factory.NewSignedInUserAsync();
        var tag = $"Pager{Guid.NewGuid():N}"[..13];
        foreach (var (suffix, power) in new[] { ("A", 10), ("B", 90), ("C", 50) })
        {
            (await client.PostAsJsonAsync("/api/heroes", NewHero($"{tag} {suffix}", powerLevel: power), Ct))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var page1 = await client.GetFromJsonAsync<PagedResult<HeroResponse>>(
            $"/api/heroes?search={tag}&page=1&pageSize=2&sortBy=PowerLevel&sortDirection=Descending", Ct);
        var page2 = await client.GetFromJsonAsync<PagedResult<HeroResponse>>(
            $"/api/heroes?search={tag}&page=2&pageSize=2&sortBy=PowerLevel&sortDirection=Descending", Ct);

        page1!.Items.Select(h => h.PowerLevel).ShouldBe([90, 50]);
        page2!.Items.Select(h => h.PowerLevel).ShouldBe([10]);
        page1.TotalCount.ShouldBe(3);
        page1.TotalPages.ShouldBe(2);
        client.Dispose();
    }

    [Fact]
    public async Task Out_of_range_paging_values_are_normalized()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        var result = await client.GetFromJsonAsync<PagedResult<HeroResponse>>("/api/heroes?page=0&pageSize=5000", Ct);

        result!.Page.ShouldBe(1);
        result.PageSize.ShouldBe(HeroQuery.MaxPageSize);
        client.Dispose();
    }

    [Fact]
    public async Task An_unknown_sort_column_is_rejected_with_400()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        (await client.GetAsync("/api/heroes?sortBy=Password", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        client.Dispose();
    }

    // AC4, AC6, AC7: full CRUD as a regular user

    [Fact]
    public async Task A_regular_user_can_create_view_update_and_find_a_hero()
    {
        var (user, client) = await factory.NewSignedInUserAsync();
        var name = $"Hero {Guid.NewGuid():N}"[..20];

        // Create: 201 with a Location header, audited with the caller's id.
        using var created = await client.PostAsJsonAsync("/api/heroes", NewHero(name), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var hero = (await created.Content.ReadFromJsonAsync<HeroResponse>(Ct))!;
        created.Headers.Location!.AbsolutePath.ShouldBe($"/api/heroes/{hero.Id}");
        hero.CreatedBy.ShouldBe(user.Id);
        hero.UpdatedBy.ShouldBeNull();

        // View through the Location.
        var loaded = await client.GetFromJsonAsync<HeroResponse>(created.Headers.Location, Ct);
        loaded!.HeroName.ShouldBe(name);

        // Update: 200, audited, creation audit unchanged.
        using var updated = await client.PutAsJsonAsync($"/api/heroes/{hero.Id}",
            UpdateFrom(NewHero(name, powerLevel: 99)), Ct);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterUpdate = (await updated.Content.ReadFromJsonAsync<HeroResponse>(Ct))!;
        afterUpdate.PowerLevel.ShouldBe(99);
        afterUpdate.UpdatedBy.ShouldBe(user.Id);
        afterUpdate.CreatedBy.ShouldBe(user.Id);

        // The list finds it.
        var search = await client.GetFromJsonAsync<PagedResult<HeroResponse>>($"/api/heroes?search={Uri.EscapeDataString(name)}", Ct);
        search!.Items.ShouldHaveSingleItem().Id.ShouldBe(hero.Id);
        client.Dispose();
    }

    [Fact]
    public async Task Input_is_normalized_like_the_domain_does()
    {
        var (_, client) = await factory.NewSignedInUserAsync();
        var name = $"Norm {Guid.NewGuid():N}"[..20];

        using var created = await client.PostAsJsonAsync("/api/heroes",
            NewHero($"  {name} ") with { State = "ny", Email = $"  {name.Replace(' ', '.')}@HeroRegistry.COM " }, Ct);

        var hero = (await created.Content.ReadFromJsonAsync<HeroResponse>(Ct))!;
        hero.HeroName.ShouldBe(name);
        hero.State.ShouldBe("NY");
        hero.Email.ShouldBe($"{name.Replace(' ', '.')}@heroregistry.com".ToLowerInvariant());
        client.Dispose();
    }

    [Fact]
    public async Task A_hero_can_keep_its_own_name_when_updated()
    {
        var (_, client) = await factory.NewSignedInUserAsync();
        var hero = await CreateAsync(client);

        using var response = await client.PutAsJsonAsync($"/api/heroes/{hero.Id}", UpdateFrom(NewHero(hero.HeroName)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.Dispose();
    }

    // Validation and conflicts

    [Fact]
    public async Task Invalid_data_returns_400_with_every_field_error()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        using var response = await client.PostAsJsonAsync("/api/heroes", NewHero("X") with
        {
            PowerLevel = 101,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            Superpowers = [],
            ZipCode = "1234",
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name)
            .ShouldBe(["heroName", "powerLevel", "birthDate", "superpowers", "zipCode"], ignoreOrder: true);
        client.Dispose();
    }

    [Fact]
    public async Task An_empty_body_reports_every_required_field()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        using var response = await client.PostAsJsonAsync("/api/heroes", new { }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("errors").EnumerateObject().Count().ShouldBe(11);
        client.Dispose();
    }

    [Fact]
    public async Task A_malformed_date_is_rejected_with_400()
    {
        var (_, client) = await factory.NewSignedInUserAsync();
        using var content = new StringContent("""{ "heroName": "Bad Date", "birthDate": "1990-13-45" }""",
            System.Text.Encoding.UTF8, "application/json");

        (await client.PostAsync("/api/heroes", content, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        client.Dispose();
    }

    [Fact]
    public async Task A_duplicate_hero_name_returns_409_naming_the_field()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        using var response = await client.PostAsJsonAsync("/api/heroes", NewHero("SUPERMAN"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("code").GetString().ShouldBe("hero.duplicate");
        problem.GetProperty("errors").TryGetProperty("heroName", out _).ShouldBeTrue();
        client.Dispose();
    }

    [Fact]
    public async Task Renaming_a_hero_to_an_existing_name_returns_409()
    {
        var (_, client) = await factory.NewSignedInUserAsync();
        var hero = await CreateAsync(client);

        using var response = await client.PutAsJsonAsync($"/api/heroes/{hero.Id}", UpdateFrom(NewHero("Spider-Man")), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        client.Dispose();
    }

    [Fact]
    public async Task Unknown_heroes_return_404()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        using var get = await client.GetAsync("/api/heroes/999999", Ct);
        using var put = await client.PutAsJsonAsync("/api/heroes/999999", UpdateFrom(NewHero("Nobody Here")), Ct);

        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await get.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("hero.not_found");
        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        client.Dispose();
    }

    // AC8: delete (administrators only)

    [Fact]
    public async Task A_regular_user_cannot_delete_a_hero()
    {
        var (_, client) = await factory.NewSignedInUserAsync();
        var hero = await CreateAsync(client);

        (await client.DeleteAsync($"/api/heroes/{hero.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/heroes/{hero.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        client.Dispose();
    }

    [Fact]
    public async Task Admin_soft_deletes_a_hero_which_then_disappears()
    {
        var (_, client) = await factory.NewSignedInUserAsync();
        var hero = await CreateAsync(client);
        using var admin = await factory.SignedInClientAsync(AdminUserName, AdminPassword);

        using var deleted = await admin.DeleteAsync($"/api/heroes/{hero.Id}", Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/heroes/{hero.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var search = await admin.GetFromJsonAsync<PagedResult<HeroResponse>>(
            $"/api/heroes?search={Uri.EscapeDataString(hero.HeroName)}", Ct);
        search!.TotalCount.ShouldBe(0);
        (await admin.DeleteAsync($"/api/heroes/{hero.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        client.Dispose();
    }

    private static async Task<HeroResponse> CreateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/heroes", NewHero($"Hero {Guid.NewGuid():N}"[..20]), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<HeroResponse>(Ct))!;
    }

    private static CreateHeroRequest NewHero(string heroName, int powerLevel = 50) => new()
    {
        HeroName = heroName,
        FirstName = "Test",
        LastName = "Hero",
        BirthDate = new DateOnly(1990, 1, 1),
        Email = $"{Guid.NewGuid():N}@heroregistry.com",
        Superpowers = ["Testing"],
        PowerLevel = powerLevel,
        Address1 = "1 Test Street",
        Address2 = null,
        City = "Manhattan",
        State = "NY",
        ZipCode = "10001",
    };

    private static UpdateHeroRequest UpdateFrom(CreateHeroRequest request) => new()
    {
        HeroName = request.HeroName,
        FirstName = request.FirstName,
        LastName = request.LastName,
        BirthDate = request.BirthDate,
        Email = request.Email,
        Superpowers = request.Superpowers,
        PowerLevel = request.PowerLevel,
        Address1 = request.Address1,
        Address2 = request.Address2,
        City = request.City,
        State = request.State,
        ZipCode = request.ZipCode,
    };
}
