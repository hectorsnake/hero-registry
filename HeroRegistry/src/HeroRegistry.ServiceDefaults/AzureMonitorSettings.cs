using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Reads the Azure Application Insights settings.
/// Application Insights is enabled only when <see cref="ConnectionStringKey"/> is set:
/// user secrets in development, environment variables (.env) under Docker Compose.
/// </summary>
public static class AzureMonitorSettings
{
    /// <summary>The standard Application Insights connection string setting.</summary>
    public const string ConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>Fraction of traces sent to Application Insights, from 0.0 to 1.0. Default: 1.0 (everything).</summary>
    public const string SamplingRatioKey = "ApplicationInsights:SamplingRatio";

    /// <summary>
    /// Returns <see langword="true"/> when a connection string is configured.
    /// A missing or empty value means "Application Insights is disabled".
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A value is present but is not an Application Insights connection string (for example a placeholder).
    /// Failing fast beats silently losing every log.
    /// </exception>
    public static bool TryGetConnectionString(
        IConfiguration configuration,
        [NotNullWhen(true)] out string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[ConnectionStringKey];

        if (string.IsNullOrWhiteSpace(value))
        {
            connectionString = null;
            return false;
        }

        if (!value.Contains("InstrumentationKey=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{ConnectionStringKey}' is set but is not a valid Application Insights connection string " +
                "(it must contain 'InstrumentationKey='). Copy the connection string from the Azure portal, " +
                "or remove the setting to run without Application Insights.");
        }

        connectionString = value.Trim();
        return true;
    }

    /// <exception cref="InvalidOperationException">The value is not a number between 0 and 1.</exception>
    public static float GetSamplingRatio(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[SamplingRatioKey];

        if (string.IsNullOrWhiteSpace(value))
        {
            return 1.0f;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio)
            && ratio is >= 0f and <= 1f)
        {
            return ratio;
        }

        throw new InvalidOperationException(
            $"'{SamplingRatioKey}' must be a number between 0.0 and 1.0, but was '{value}'.");
    }
}
