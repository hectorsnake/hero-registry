using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Placed in the Hosting namespace so `builder.AddServiceDefaults()` is available without extra usings.
namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds common services: service discovery, resilience, health checks and OpenTelemetry.
/// Based on the .NET Aspire ServiceDefaults template.
/// </summary>
public static class Extensions
{
    /// <summary>Liveness endpoint: the process is running.</summary>
    public const string AlivenessEndpointPath = "/alive";

    /// <summary>Readiness endpoint: the process and all its dependencies (database) are healthy.</summary>
    public const string HealthEndpointPath = "/health";

    /// <summary>Tag for health checks that only verify the process itself is responsive.</summary>
    public const string LiveTag = "live";

    /// <summary>
    /// Every ActivitySource and Meter named "HeroRegistry.*" is collected,
    /// for example the data layer's "HeroRegistry.Data".
    /// </summary>
    public const string HeroRegistryTelemetrySources = "HeroRegistry.*";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var openTelemetry = builder.Services.AddOpenTelemetry();

        // Service name = Application Insights "cloud role name". Aspire sets OTEL_SERVICE_NAME to the
        // resource name ("api") and its dashboard relies on it, so it is only defaulted when absent.
        if (string.IsNullOrWhiteSpace(builder.Configuration["OTEL_SERVICE_NAME"]))
        {
            openTelemetry.ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName));
        }

        openTelemetry
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(HeroRegistryTelemetrySources))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(HeroRegistryTelemetrySources)
                // Requests to endpoints: Application Insights "requests".
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Keep health probes out of the traces.
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                        && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation()
                // SQL commands: Application Insights "dependencies" (type SQL).
                .AddHeroRegistrySqlClientInstrumentation());

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Aspire dashboard: Aspire sets OTEL_EXPORTER_OTLP_ENDPOINT. Outside Aspire it is not set.
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Azure Application Insights: logs (traces), requests, dependencies, exceptions and metrics.
        // Both exporters can be active at the same time; they receive the same telemetry.
        if (AzureMonitorSettings.TryGetConnectionString(builder.Configuration, out var connectionString))
        {
            var samplingRatio = AzureMonitorSettings.GetSamplingRatio(builder.Configuration);

            builder.Services.AddOpenTelemetry().UseAzureMonitorExporter(options =>
            {
                options.ConnectionString = connectionString;
                options.SamplingRatio = samplingRatio;
                // TracesPerSecond (rate-limited sampling) takes precedence over SamplingRatio when set,
                // so it is cleared to keep a single, predictable setting.
                options.TracesPerSecond = null;
            });
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Health endpoints are mapped in Development only for now.
        // Exposing them in other environments (with care) is part of T11.
        if (app.Environment.IsDevelopment())
        {
            // Readiness: every registered health check must pass.
            // Anonymous: probes (Aspire, Docker) carry no token.
            app.MapHealthChecks(HealthEndpointPath).AllowAnonymous();

            // Liveness: only checks tagged "live".
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(LiveTag),
            }).AllowAnonymous();
        }

        return app;
    }
}
