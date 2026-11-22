using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// The API runs in Development during tests, which loads the developer's user secrets, including a real
/// APPLICATIONINSIGHTS_CONNECTION_STRING. Tests must never send telemetry to that Azure resource.
/// </summary>
public class TelemetryIsolationTests(HeroRegistryApiFactory factory) : IClassFixture<HeroRegistryApiFactory>
{
    [Fact]
    public void Tests_never_export_to_Application_Insights()
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<AzureMonitorExporterOptions>>()
            .Get(Options.DefaultName);

        options.ConnectionString.ShouldBeNullOrWhiteSpace();
    }
}
