using System.Text;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Security;

/// <summary>
/// Access token settings, from the <c>Jwt</c> configuration section. The signing key is a secret:
/// it comes from user secrets (API run on its own), the Aspire AppHost (<c>Jwt__Key</c>, generated)
/// or <c>.env</c> (Docker Compose), never from a committed file.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "hero-registry-api";

    public string Audience { get; set; } = "hero-registry-ui";

    public string Key { get; set; } = string.Empty;

    /// <summary>Token lifetime. There are no refresh tokens (out of scope), so users sign in again after it.</summary>
    public int ExpirationMinutes { get; set; } = 60;
}

/// <summary>Rejects missing or weak settings at startup (registered with ValidateOnStart).</summary>
internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            failures.Add(
                "Jwt:Key is not configured. Run the API through the Aspire AppHost (it generates one), or set one with " +
                "'dotnet user-secrets set \"Jwt:Key\" \"<at least 32 random characters>\" --project HeroRegistry/src/HeroRegistry.Api'.");
        }
        else if (Encoding.UTF8.GetByteCount(options.Key) < JwtOptions.MinimumKeyBytes)
        {
            failures.Add($"Jwt:Key must be at least {JwtOptions.MinimumKeyBytes} bytes long.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (options.ExpirationMinutes is < 1 or > 1440)
        {
            failures.Add("Jwt:ExpirationMinutes must be between 1 and 1440 (24 hours).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
