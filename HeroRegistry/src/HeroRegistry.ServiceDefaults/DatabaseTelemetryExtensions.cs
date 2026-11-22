using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Client-side tracing of SQL Server commands (Microsoft.Data.SqlClient). Every SELECT, INSERT and UPDATE
/// becomes a dependency span (Application Insights <c>dependencies</c> table, Aspire dashboard traces)
/// with the command text and duration. Nothing is installed or changed on SQL Server itself.
/// </summary>
/// <remarks>
/// Privacy and server-side impact. These defaults of the instrumentation are relied upon, and are only
/// switchable through experimental environment variables, which this project never sets:
/// <list type="bullet">
///   <item>Parameter values are <b>not</b> captured. They can hold personal data (names, emails,
///   addresses, password hashes). The command text is safe because every statement is parameterized.</item>
///   <item>Trace context propagation is <b>off</b>
///   (<c>OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_CONTEXT_PROPAGATION</c>), so no extra
///   <c>SET CONTEXT_INFO</c> command is sent to SQL Server.</item>
/// </list>
/// </remarks>
public static class DatabaseTelemetryExtensions
{
    public static TracerProviderBuilder AddHeroRegistrySqlClientInstrumentation(this TracerProviderBuilder tracing) =>
        tracing.AddSqlClientInstrumentation(options =>
            // SqlException messages can echo data, for example
            // "The duplicate key value is (someone@example.com)", so exceptions are not attached.
            // The span is still marked as failed, with the error type.
            options.RecordException = false);
}
