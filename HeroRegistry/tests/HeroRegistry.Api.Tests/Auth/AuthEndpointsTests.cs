using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeroRegistry.Api.Tests.Fixtures;
using HeroRegistry.Application.Users;
using Microsoft.IdentityModel.JsonWebTokens;
using static HeroRegistry.Api.Tests.Fixtures.ApiClientExtensions;

namespace HeroRegistry.Api.Tests.Auth;

/// <summary>
/// /api/auth end to end: real SQL Server (migrated and seeded by the API), real BCrypt and JWT.
/// Covers AC1 (registration), AC2 (login) and AC3 (anonymous vs. protected endpoints).
/// </summary>
public class AuthEndpointsTests(DatabaseApiFactory factory) : IClassFixture<DatabaseApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Register (anonymous)

    [Fact]
    public async Task Register_creates_a_regular_user_and_returns_201()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterUserRequest { UserName = "peter.parker", Email = "Peter@DailyBugle.com", Password = StrongPassword }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("userName").GetString().ShouldBe("peter.parker");
        body.GetProperty("email").GetString().ShouldBe("peter@dailybugle.com");
        body.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
        body.TryGetProperty("password", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Registering_the_same_user_again_returns_409_naming_the_field()
    {
        using var client = factory.CreateClient();
        var user = await client.RegisterUserAsync();

        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterUserRequest { UserName = user.UserName.ToUpperInvariant(), Email = "other@heroregistry.com", Password = StrongPassword }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("code").GetString().ShouldBe("user.duplicate");
        problem.GetProperty("errors").TryGetProperty("userName", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Invalid_registration_returns_400_with_every_field_error()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterUserRequest { UserName = "x", Email = "not-an-email", Password = "weak" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name)
            .ShouldBe(["userName", "email", "password"], ignoreOrder: true);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("weak");
    }

    // Login (anonymous)

    [Fact]
    public async Task The_seeded_Admin_logs_in_and_gets_a_token_with_the_Admin_role()
    {
        using var client = factory.CreateClient();

        using var response = await client.LoginAsync(AdminUserName, AdminPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Ct))!;
        auth.User.IsAdmin.ShouldBeTrue();
        auth.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
        new JsonWebTokenHandler().ReadJsonWebToken(auth.AccessToken).GetClaim("role").Value.ShouldBe("Admin");
        (await response.ReadJsonAsync()).GetProperty("tokenType").GetString().ShouldBe("Bearer");
    }

    [Fact]
    public async Task A_newly_registered_user_logs_in_with_the_User_role()
    {
        using var client = factory.CreateClient();
        var user = await client.RegisterUserAsync();

        using var response = await client.LoginAsync(user.UserName, StrongPassword);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Ct))!;
        new JsonWebTokenHandler().ReadJsonWebToken(auth.AccessToken).GetClaim("role").Value.ShouldBe("User");
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_user_return_the_same_401()
    {
        using var client = factory.CreateClient();

        using var wrongPassword = await client.LoginAsync(AdminUserName, "wrong");
        using var unknownUser = await client.LoginAsync("nobody", "Admin");

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var first = await wrongPassword.ReadJsonAsync();
        var second = await unknownUser.ReadJsonAsync();
        first.GetProperty("code").GetString().ShouldBe("auth.invalid_credentials");
        second.GetProperty("code").GetString().ShouldBe("auth.invalid_credentials");
        first.GetProperty("title").GetString().ShouldBe(second.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_with_missing_fields_returns_400()
    {
        using var client = factory.CreateClient();

        using var response = await client.LoginAsync(AdminUserName, "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // Me (protected)

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer");
    }

    [Fact]
    public async Task Me_with_a_token_returns_the_signed_in_user()
    {
        using var client = await factory.SignedInClientAsync(AdminUserName, AdminPassword);

        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me", Ct);

        me!.UserName.ShouldBe("Admin");
        me.IsAdmin.ShouldBeTrue();
    }

    [Theory]
    [InlineData("not.a.jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.forged-signature")]
    public async Task An_invalid_or_forged_token_returns_401(string token)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_stays_anonymous_while_every_other_endpoint_requires_a_token()
    {
        using var client = factory.CreateClient();

        using var health = await client.GetAsync("/health", Ct);
        using var users = await client.GetAsync("/api/users", Ct);

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        users.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
