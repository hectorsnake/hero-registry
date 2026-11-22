using System.Text;

namespace HeroRegistry.Application.Users;

/// <summary>Refresh token settings (the <c>RefreshToken</c> section), validated at startup.</summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>How long a refresh token (and so a sign-in) lasts without being used. 1 to 30 days.</summary>
    public int LifetimeDays { get; set; } = 7;

    /// <summary>
    /// How long after a rotation the old token is treated as a race (two tabs) rather than a theft.
    /// 0 to 120 seconds.
    /// </summary>
    public int ReuseGraceSeconds { get; set; } = 30;

    /// <summary>Name of the HttpOnly cookie that carries the refresh token.</summary>
    public string CookieName { get; set; } = "hr_refresh";

    public TimeSpan Lifetime => TimeSpan.FromDays(LifetimeDays);

    public TimeSpan ReuseGrace => TimeSpan.FromSeconds(ReuseGraceSeconds);

    /// <summary>Every problem with the settings (empty when valid).</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (LifetimeDays is < 1 or > 30)
        {
            problems.Add("RefreshToken:LifetimeDays must be between 1 and 30.");
        }

        if (ReuseGraceSeconds is < 0 or > 120)
        {
            problems.Add("RefreshToken:ReuseGraceSeconds must be between 0 and 120.");
        }

        if (string.IsNullOrWhiteSpace(CookieName) || !CookieName.All(IsCookieNameChar))
        {
            problems.Add("RefreshToken:CookieName must be a non-empty cookie name (letters, digits, '-' or '_').");
        }

        return problems;
    }

    private static bool IsCookieNameChar(char c) => c is '-' or '_' || (c < 128 && char.IsAsciiLetterOrDigit(c));
}

/// <summary>
/// The outcome of a login or refresh for the API layer: the JSON body (<see cref="AuthResponse"/>) and
/// the refresh token, which the API puts in the HttpOnly cookie and **never** in the body.
/// </summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Response = ").Append(Response)
            .Append(", RefreshToken = ***, RefreshTokenExpiresAt = ").Append(RefreshTokenExpiresAt);
        return true;
    }
}
