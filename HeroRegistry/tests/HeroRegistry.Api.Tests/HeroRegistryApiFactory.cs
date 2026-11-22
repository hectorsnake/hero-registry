using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// Hosts the API for integration tests (Development environment).
/// Application Insights is switched off, because user secrets may hold a real connection string.
/// </summary>
public class HeroRegistryApiFactory : WebApplicationFactory<Program>
{
    /// <summary>JWT signing key used by the API under test (at least 32 bytes).</summary>
    public const string TestJwtKey = "api-tests-signing-key-at-least-32-bytes-long";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Whitespace means "not configured" (see AzureMonitorSettings).
        builder.UseSetting(AzureMonitorSettings.ConnectionStringKey, " ");

        // The real key is a secret (user secrets, Aspire, .env); tests use their own.
        builder.UseSetting("Jwt:Key", TestJwtKey);
    }
}
