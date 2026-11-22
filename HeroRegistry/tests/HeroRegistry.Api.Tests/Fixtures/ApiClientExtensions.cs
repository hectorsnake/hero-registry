using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HeroRegistry.Application.Users;
using HeroRegistry.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Api.Tests.Fixtures;

/// <summary>Helpers to register, sign in and read problem details in endpoint tests.</summary>
internal static class ApiClientExtensions
{
    public const string AdminUserName = "Admin";
    public const string AdminPassword = "Admin";
    public const string StrongPassword = "Sp1der-Sense!";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Registers a new regular user with a unique name and returns it.</summary>
    public static async Task<UserResponse> RegisterUserAsync(this HttpClient client, string? userName = null)
    {
        userName ??= $"user_{Guid.NewGuid():N}"[..20];
        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterUserRequest { UserName = userName, Email = $"{userName}@heroregistry.com", Password = StrongPassword }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<UserResponse>(Ct))!;
    }

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string userName, string password) =>
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { UserName = userName, Password = password }, Ct);

    /// <summary>A client carrying the bearer token of the given user.</summary>
    public static async Task<HttpClient> SignedInClientAsync(
        this WebApplicationFactory<Program> factory, string userName, string password = StrongPassword)
    {
        var client = factory.CreateClient();
        using var response = await client.LoginAsync(userName, password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>A newly registered regular user and a client signed in as that user.</summary>
    public static async Task<(UserResponse User, HttpClient Client)> NewSignedInUserAsync(this WebApplicationFactory<Program> factory)
    {
        using var anonymous = factory.CreateClient();
        var user = await anonymous.RegisterUserAsync();
        return (user, await factory.SignedInClientAsync(user.UserName));
    }

    /// <summary>A token signed with the test key, without a database (for authentication-only tests).</summary>
    public static string TokenFor(int userId, string userName, bool isAdmin = false)
    {
        var user = Domain.Users.User.Register(userName, $"{userName}@heroregistry.com", "$2a$11$hash", DateTime.UtcNow);
        typeof(Domain.Users.User).GetProperty(nameof(Domain.Users.User.Id))!.SetValue(user, userId);
        if (isAdmin)
        {
            typeof(Domain.Users.User).GetProperty(nameof(Domain.Users.User.IsAdmin))!.SetValue(user, true);
        }

        var service = new JwtTokenService(
            Options.Create(new JwtOptions { Key = HeroRegistryApiFactory.TestJwtKey }), TimeProvider.System);
        return service.CreateToken(user).Token;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }
}
