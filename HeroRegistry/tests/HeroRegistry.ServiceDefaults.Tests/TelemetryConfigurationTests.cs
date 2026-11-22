using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace HeroRegistry.ServiceDefaults.Tests;

/// <summary>
/// AddServiceDefaults sends telemetry to Application Insights only when it is configured,
/// so tests, CI and fresh clones never need (or leak to) an Azure resource.
/// </summary>
public class TelemetryConfigurationTests
{
    private const string ApplicationName = "HeroRegistry.Telemetry.Tests";

    private const string ValidConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:1/";

    [Fact]
    public void Application_Insights_is_not_registered_without_a_connection_string()
    {
        var builder = CreateBuilder();

        builder.AddServiceDefaults();

        HasAzureMonitorExporter(builder.Services).ShouldBeFalse();
    }

    [Fact]
    public void Application_Insights_is_registered_when_a_connection_string_is_set()
    {
        var builder = CreateBuilder((AzureMonitorSettings.ConnectionStringKey, ValidConnectionString));

        builder.AddServiceDefaults();

        HasAzureMonitorExporter(builder.Services).ShouldBeTrue();
    }

    [Fact]
    public void The_exporter_uses_the_configured_connection_string_and_sampling_ratio()
    {
        var builder = CreateBuilder(
            (AzureMonitorSettings.ConnectionStringKey, ValidConnectionString),
            (AzureMonitorSettings.SamplingRatioKey, "0.5"));
        builder.AddServiceDefaults();

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptionsMonitor<AzureMonitorExporterOptions>>()
            .Get(Options.DefaultName);

        options.ConnectionString.ShouldBe(ValidConnectionString);
        options.SamplingRatio.ShouldBe(0.5f);
        options.TracesPerSecond.ShouldBeNull();
    }

    [Fact]
    public void A_placeholder_connection_string_stops_the_application_at_startup()
    {
        var builder = CreateBuilder((AzureMonitorSettings.ConnectionStringKey, "<use secrets>"));

        Should.Throw<InvalidOperationException>(() => builder.AddServiceDefaults());
    }

    [Fact]
    public void Service_name_defaults_to_the_application_name()
    {
        var builder = CreateBuilder();
        builder.AddServiceDefaults();

        using var host = builder.Build();

        ServiceName(host).ShouldBe(ApplicationName);
    }

    [Fact]
    public void Service_name_from_Aspire_is_kept_so_the_dashboard_can_match_the_resource()
    {
        var builder = CreateBuilder(("OTEL_SERVICE_NAME", "api"));
        builder.AddServiceDefaults();

        using var host = builder.Build();

        ServiceName(host).ShouldBe("api");
    }

    private static HostApplicationBuilder CreateBuilder(params (string Key, string? Value)[] settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = ApplicationName,
            EnvironmentName = Environments.Production,
        });
        builder.Configuration.AddInMemoryCollection(
            settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        return builder;
    }

    private static string? ServiceName(IHost host) =>
        host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes
            .FirstOrDefault(a => a.Key == "service.name").Value as string;

    /// <summary>Any service registration that comes from the Azure Monitor exporter assembly.</summary>
    private static bool HasAzureMonitorExporter(IServiceCollection services)
    {
        var exporterAssembly = typeof(AzureMonitorExporterOptions).Assembly;

        bool FromExporter(Type? type) =>
            type is not null
            && (type.Assembly == exporterAssembly || type.GenericTypeArguments.Any(FromExporter));

        return services.Any(d =>
            FromExporter(d.ServiceType)
            || FromExporter(d.ImplementationType)
            || FromExporter(d.ImplementationInstance?.GetType())
            || FromExporter(d.ImplementationFactory?.Method.DeclaringType));
    }
}
