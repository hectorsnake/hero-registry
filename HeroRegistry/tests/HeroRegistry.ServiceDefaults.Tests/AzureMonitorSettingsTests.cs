using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HeroRegistry.ServiceDefaults.Tests;

public class AzureMonitorSettingsTests
{
    private const string ValidConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:1/";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_empty_connection_string_means_disabled(string? value)
    {
        AzureMonitorSettings.TryGetConnectionString(Config(AzureMonitorSettings.ConnectionStringKey, value), out var connectionString)
            .ShouldBeFalse();
        connectionString.ShouldBeNull();
    }

    [Fact]
    public void A_valid_connection_string_enables_Application_Insights()
    {
        AzureMonitorSettings.TryGetConnectionString(
                Config(AzureMonitorSettings.ConnectionStringKey, $"  {ValidConnectionString}  "), out var connectionString)
            .ShouldBeTrue();
        connectionString.ShouldBe(ValidConnectionString);
    }

    [Theory]
    [InlineData("<use secrets>")]
    [InlineData("not-a-connection-string")]
    public void A_placeholder_or_invalid_value_fails_fast_with_a_clear_message(string value)
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => AzureMonitorSettings.TryGetConnectionString(Config(AzureMonitorSettings.ConnectionStringKey, value), out _));

        exception.Message.ShouldContain(AzureMonitorSettings.ConnectionStringKey);
        exception.Message.ShouldNotContain(value); // never echo a (possibly secret) value
    }

    [Theory]
    [InlineData(null, 1.0f)]
    [InlineData("", 1.0f)]
    [InlineData("0", 0.0f)]
    [InlineData("0.25", 0.25f)]
    [InlineData("1", 1.0f)]
    public void Sampling_ratio_defaults_to_everything_and_accepts_0_to_1(string? value, float expected)
    {
        AzureMonitorSettings.GetSamplingRatio(Config(AzureMonitorSettings.SamplingRatioKey, value)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.5")]
    [InlineData("half")]
    public void Sampling_ratio_outside_0_to_1_is_rejected(string value)
    {
        Should.Throw<InvalidOperationException>(
                () => AzureMonitorSettings.GetSamplingRatio(Config(AzureMonitorSettings.SamplingRatioKey, value)))
            .Message.ShouldContain(AzureMonitorSettings.SamplingRatioKey);
    }

    private static IConfiguration Config(string key, string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>(key, value)])
            .Build();
}
