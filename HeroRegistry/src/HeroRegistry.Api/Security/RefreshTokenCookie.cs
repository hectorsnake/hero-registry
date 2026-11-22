using HeroRegistry.Application.Users;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Api.Security;

/// <summary>
/// The refresh token cookie (T09.1, AC12). JavaScript cannot read it (HttpOnly), it only travels over
/// HTTPS (Secure), never on cross-site requests (SameSite=Strict, which also blocks CSRF on the cookie
/// endpoints), and only to <c>/api/auth</c>, so other API calls never carry it.
/// </summary>
public sealed class RefreshTokenCookie(IOptions<RefreshTokenOptions> options)
{
    /// <summary>The only path the browser sends the cookie to: login, refresh and logout live here.</summary>
    public const string Path = "/api/auth";

    private string Name => options.Value.CookieName;

    public string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    public void Write(HttpResponse response, string refreshToken, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Append(Name, refreshToken, CookieOptions(new DateTimeOffset(expiresAt, TimeSpan.Zero)));
    }

    /// <summary>Tells the browser to drop the cookie (same name, path and attributes, expired).</summary>
    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Name, CookieOptions(expires: null));
    }

    private static CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true, // strictly necessary for signing in, so not subject to consent
    };
}
