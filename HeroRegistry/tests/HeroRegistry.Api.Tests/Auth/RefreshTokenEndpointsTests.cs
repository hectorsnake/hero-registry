using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeroRegistry.Api.Tests.Fixtures;
using HeroRegistry.Application.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;
using static HeroRegistry.Api.Tests.Fixtures.ApiClientExtensions;

namespace HeroRegistry.Api.Tests.Auth;

/// <summary>
/// The session lifecycle end to end (T09.1, AC12): login sets the HttpOnly refresh cookie, refresh rotates it,
/// reuse of a rotated cookie revokes the family, logout ends the session. Real SQL Server, a fake clock.
/// Cookies are handled by hand: the test client talks plain HTTP, where a browser would not send a
/// Secure cookie, and the assertions need the raw Set-Cookie attributes anyway.
/// </summary>
public sealed class RefreshTokenEndpointsTests : IClassFixture<DatabaseApiFactory>, IDisposable
{
    private const string CookieName = "hr_refresh";

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly FakeLoggerProvider _logs = new();
    private readonly WebApplicationFactory<Program> _factory;

    public RefreshTokenEndpointsTests(DatabaseApiFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            services.AddLogging(logging => logging.AddProvider(_logs).SetMinimumLevel(LogLevel.Trace));
        }));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _factory.Dispose();
        _logs.Dispose();
    }

    [Fact]
    public async Task Login_sets_a_secure_HttpOnly_cookie_and_keeps_the_refresh_token_out_of_the_body()
    {
        using var client = Client();

        using var response = await client.LoginAsync(AdminUserName, AdminPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = RefreshCookie(response);
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Secure.ShouldBeTrue();
        cookie.SameSite.ShouldBe(Microsoft.Net.Http.Headers.SameSiteMode.Strict);
        cookie.Path.ToString().ShouldBe("/api/auth");
        cookie.Expires!.Value.ShouldBe(_clock.GetUtcNow().AddDays(7), TimeSpan.FromSeconds(1));
        cookie.Value.Length.ShouldBe(43);

        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain(cookie.Value.ToString());
        body.ShouldNotContain("refresh", Case.Insensitive);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_returns_a_new_access_token_that_works_and_rotates_the_cookie()
    {
        using var client = Client();
        // No clock advance: JwtBearer checks "not before" against the real clock.
        var (login, cookie) = await LoginAsync(client);

        using var response = await RefreshAsync(client, cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Ct))!;
        auth.AccessToken.ShouldNotBe(login.AccessToken);
        auth.User.UserName.ShouldBe(AdminUserName);
        RefreshCookie(response).Value.ToString().ShouldNotBe(cookie);

        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        (await client.SendAsync(me, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Replaying_a_rotated_cookie_after_the_grace_window_revokes_the_whole_session()
    {
        using var client = Client();
        var (_, stolen) = await LoginAsync(client);
        using var first = await RefreshAsync(client, stolen);
        var current = RefreshCookie(first).Value.ToString();
        _clock.Advance(TimeSpan.FromSeconds(31));

        using var replay = await RefreshAsync(client, stolen);

        await ShouldBeProblemAsync(replay, "auth.refresh_invalid");
        RefreshCookie(replay).Expires!.Value.ShouldBeLessThan(DateTimeOffset.UtcNow); // cookie deleted
        using var legitimate = await RefreshAsync(client, current);
        await ShouldBeProblemAsync(legitimate, "auth.refresh_invalid"); // the family is gone
    }

    [Fact]
    public async Task Replaying_within_the_grace_window_is_a_race_that_keeps_the_session()
    {
        using var client = Client();
        var (_, cookie) = await LoginAsync(client);
        using var winner = await RefreshAsync(client, cookie);
        var current = RefreshCookie(winner).Value.ToString();
        _clock.Advance(TimeSpan.FromSeconds(5));

        using var loser = await RefreshAsync(client, cookie);

        await ShouldBeProblemAsync(loser, "auth.refresh_race");
        loser.Headers.Contains(HeaderNames.SetCookie).ShouldBeFalse(); // the newer cookie is kept
        using var retry = await RefreshAsync(client, current);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_a_401_problem()
    {
        using var client = Client();

        using var response = await client.PostAsync("/api/auth/refresh", content: null, Ct);

        await ShouldBeProblemAsync(response, "auth.refresh_invalid");
    }

    [Fact]
    public async Task Refresh_with_an_expired_cookie_is_rejected()
    {
        using var client = Client();
        var (_, cookie) = await LoginAsync(client);
        _clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1)); // DATETIME2(3) rounds to the millisecond

        using var response = await RefreshAsync(client, cookie);

        await ShouldBeProblemAsync(response, "auth.refresh_invalid");
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_deletes_the_cookie()
    {
        using var client = Client();
        var (_, cookie) = await LoginAsync(client);

        using var logout = await SendWithCookieAsync(client, "/api/auth/logout", cookie);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var deleted = RefreshCookie(logout);
        deleted.Path.ToString().ShouldBe("/api/auth");
        deleted.Expires!.Value.ShouldBeLessThan(DateTimeOffset.UtcNow);
        using var refresh = await RefreshAsync(client, cookie);
        await ShouldBeProblemAsync(refresh, "auth.refresh_invalid");
    }

    [Fact]
    public async Task Logout_without_a_session_still_succeeds()
    {
        using var client = Client();

        using var response = await client.PostAsync("/api/auth/logout", content: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deleting_a_user_ends_their_sessions()
    {
        using var client = Client();
        var user = await client.RegisterUserAsync();
        using var login = await client.LoginAsync(user.UserName, StrongPassword);
        var cookie = RefreshCookie(login).Value.ToString();
        using var admin = await _factory.SignedInClientAsync(AdminUserName, AdminPassword);
        _clock.Advance(TimeSpan.FromSeconds(1)); // a frozen clock would delete "before" the rounded CreatedAt

        (await admin.DeleteAsync($"/api/users/{user.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var refresh = await RefreshAsync(client, cookie);
        await ShouldBeProblemAsync(refresh, "auth.refresh_invalid");
    }

    [Fact]
    public async Task Refresh_tokens_never_appear_in_the_logs()
    {
        using var client = Client();
        var (_, cookie) = await LoginAsync(client);
        using var refreshed = await RefreshAsync(client, cookie);
        var rotated = RefreshCookie(refreshed).Value.ToString();
        _clock.Advance(TimeSpan.FromMinutes(5));
        using var reuse = await RefreshAsync(client, cookie); // logs the reuse warning

        var logged = _logs.Collector.GetSnapshot()
            .SelectMany(r => (r.StructuredState ?? []).Select(kv => kv.Value).Append(r.Message))
            .ToList();
        logged.ShouldContain(m => m != null && m.Contains("reuse detected", StringComparison.Ordinal));
        logged.ShouldAllBe(m => m == null || (!m.Contains(cookie) && !m.Contains(rotated)));
    }

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static async Task<(AuthResponse Auth, string Cookie)> LoginAsync(HttpClient client)
    {
        using var response = await client.LoginAsync(AdminUserName, AdminPassword);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return ((await response.Content.ReadFromJsonAsync<AuthResponse>(Ct))!, RefreshCookie(response).Value.ToString());
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie) =>
        SendWithCookieAsync(client, "/api/auth/refresh", cookie);

    private static async Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(HeaderNames.Cookie, $"{CookieName}={cookie}");
        return await client.SendAsync(request, Ct);
    }

    private static SetCookieHeaderValue RefreshCookie(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values).ShouldBeTrue("no Set-Cookie header");
        return SetCookieHeaderValue.ParseList(values!.ToList()).Single(c => c.Name == CookieName);
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe(code);
    }
}
