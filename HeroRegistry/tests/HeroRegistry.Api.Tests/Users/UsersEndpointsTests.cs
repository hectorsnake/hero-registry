using System.Net;
using System.Net.Http.Json;
using HeroRegistry.Api.Tests.Fixtures;
using HeroRegistry.Application.Users;
using static HeroRegistry.Api.Tests.Fixtures.ApiClientExtensions;

namespace HeroRegistry.Api.Tests.Users;

/// <summary>
/// /api/users end to end: listing and deleting are for administrators; updating (AC11) is for the
/// account owner or an administrator.
/// </summary>
public class UsersEndpointsTests(DatabaseApiFactory factory) : IClassFixture<DatabaseApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // List (admin)

    [Fact]
    public async Task Listing_users_returns_403_for_a_regular_user_and_200_for_Admin()
    {
        var (_, regular) = await factory.NewSignedInUserAsync();
        using var admin = await factory.SignedInClientAsync(AdminUserName, AdminPassword);

        using var forbidden = await regular.GetAsync("/api/users", Ct);
        var users = await admin.GetFromJsonAsync<List<UserResponse>>("/api/users", Ct);

        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        users!.ShouldContain(u => u.UserName == "Admin" && u.IsAdmin);
        regular.Dispose();
    }

    // Update (AC11)

    [Fact]
    public async Task A_user_can_update_their_own_account_and_still_log_in()
    {
        var (user, client) = await factory.NewSignedInUserAsync();
        var newName = $"renamed_{Guid.NewGuid():N}"[..20];

        using var response = await client.PutAsJsonAsync($"/api/users/{user.Id}",
            new UpdateUserRequest { UserName = newName, Email = $"{newName}@avengers.org" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserResponse>(Ct))!.UserName.ShouldBe(newName);

        // Same password, new user name.
        using var anonymous = factory.CreateClient();
        using var login = await anonymous.LoginAsync(newName, StrongPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.Dispose();
    }

    [Fact]
    public async Task A_regular_user_cannot_update_another_account()
    {
        var (other, _) = await factory.NewSignedInUserAsync();
        var (_, client) = await factory.NewSignedInUserAsync();

        using var response = await client.PutAsJsonAsync($"/api/users/{other.Id}",
            new UpdateUserRequest { UserName = "hijacked", Email = "hijacked@evil.com" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        client.Dispose();
    }

    [Fact]
    public async Task A_regular_user_gets_403_not_404_for_an_id_that_does_not_exist()
    {
        var (_, client) = await factory.NewSignedInUserAsync();

        using var response = await client.PutAsJsonAsync("/api/users/999999",
            new UpdateUserRequest { UserName = "whoever", Email = "whoever@heroregistry.com" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        client.Dispose();
    }

    [Fact]
    public async Task Admin_can_update_any_account()
    {
        var (other, _) = await factory.NewSignedInUserAsync();
        using var admin = await factory.SignedInClientAsync(AdminUserName, AdminPassword);
        var newName = $"by_admin_{Guid.NewGuid():N}"[..20];

        using var response = await admin.PutAsJsonAsync($"/api/users/{other.Id}",
            new UpdateUserRequest { UserName = newName, Email = $"{newName}@heroregistry.com" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserResponse>(Ct))!.Id.ShouldBe(other.Id);
    }

    [Fact]
    public async Task Updating_without_a_token_returns_401()
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/api/users/1",
            new UpdateUserRequest { UserName = "x_user", Email = "x@heroregistry.com" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_updating_an_unknown_account_gets_404()
    {
        using var admin = await factory.SignedInClientAsync(AdminUserName, AdminPassword);

        using var response = await admin.PutAsJsonAsync("/api/users/999999",
            new UpdateUserRequest { UserName = "whoever", Email = "whoever@heroregistry.com" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("user.not_found");
    }

    [Fact]
    public async Task Taking_another_accounts_user_name_returns_409()
    {
        var (user, client) = await factory.NewSignedInUserAsync();

        using var response = await client.PutAsJsonAsync($"/api/users/{user.Id}",
            new UpdateUserRequest { UserName = "admin", Email = user.Email }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("userName", out _).ShouldBeTrue();
        client.Dispose();
    }

    [Fact]
    public async Task An_invalid_email_returns_400()
    {
        var (user, client) = await factory.NewSignedInUserAsync();

        using var response = await client.PutAsJsonAsync($"/api/users/{user.Id}",
            new UpdateUserRequest { UserName = user.UserName, Email = "not-an-email" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();
        client.Dispose();
    }

    // Delete (admin)

    [Fact]
    public async Task Admin_can_delete_a_user_who_then_cannot_log_in()
    {
        var (user, _) = await factory.NewSignedInUserAsync();
        using var admin = await factory.SignedInClientAsync(AdminUserName, AdminPassword);

        using var response = await admin.DeleteAsync($"/api/users/{user.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var anonymous = factory.CreateClient();
        (await anonymous.LoginAsync(user.UserName, StrongPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_regular_user_cannot_delete_users()
    {
        var (other, _) = await factory.NewSignedInUserAsync();
        var (_, client) = await factory.NewSignedInUserAsync();

        (await client.DeleteAsync($"/api/users/{other.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        client.Dispose();
    }

    [Fact]
    public async Task Admin_cannot_delete_their_own_account()
    {
        using var admin = await factory.SignedInClientAsync(AdminUserName, AdminPassword);
        var me = await admin.GetFromJsonAsync<UserResponse>("/api/auth/me", Ct);

        using var response = await admin.DeleteAsync($"/api/users/{me!.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("id", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Deleting_an_unknown_user_returns_404()
    {
        using var admin = await factory.SignedInClientAsync(AdminUserName, AdminPassword);

        (await admin.DeleteAsync("/api/users/999999", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
