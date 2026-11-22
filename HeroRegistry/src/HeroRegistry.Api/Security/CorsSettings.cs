using Microsoft.Extensions.Options;

namespace HeroRegistry.Api.Security;

/// <summary>
/// Browser origins allowed to call the API (the <c>Cors</c> configuration section). The defaults cover
/// the Angular dev server (http://localhost:4200) and the Docker Compose UI (http://localhost:8080).
/// When the UI is served from the API's own origin (dev proxy, nginx), CORS is not even needed.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "ui";

    /// <summary>Exact origins: scheme, host and optional port, without a trailing slash. Empty entries are ignored.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    internal string[] EffectiveOrigins => AllowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
}

/// <summary>Rejects wildcards and malformed origins at startup, so a typo never silently opens or breaks the UI.</summary>
internal sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        var failures = options.EffectiveOrigins
            .Where(origin => !IsOrigin(origin))
            .Select(origin =>
                $"Cors:AllowedOrigins contains '{origin}', which is not an origin. Use scheme://host[:port], " +
                "for example http://localhost:4200, without a path, a trailing slash or a wildcard.")
            .ToList();

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && !value.EndsWith('/');
}

public static class CorsExtensions
{
    /// <summary>
    /// The <c>ui</c> policy: only the configured origins, any header (Authorization, Content-Type), the
    /// verbs the API uses, the Location header exposed (201 responses), and no credentials. The access
    /// token travels in the Authorization header, not in cookies.
    /// </summary>
    public static IServiceCollection AddUiCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();

        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<CorsSettings>>((cors, settings) =>
                cors.AddPolicy(CorsSettings.PolicyName, policy => policy
                    .WithOrigins(settings.Value.EffectiveOrigins)
                    .AllowAnyHeader()
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithExposedHeaders("Location")
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }
}
