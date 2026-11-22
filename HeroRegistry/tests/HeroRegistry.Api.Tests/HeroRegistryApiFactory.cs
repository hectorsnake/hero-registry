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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Whitespace means "not configured" (see AzureMonitorSettings).
        builder.UseSetting(AzureMonitorSettings.ConnectionStringKey, " ");
    }
}
