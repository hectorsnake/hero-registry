using System.Text.Json.Serialization;
using HeroRegistry.Api.ErrorHandling;
using HeroRegistry.Api.OpenApi;
using HeroRegistry.Api.Security;
using HeroRegistry.Application;
using HeroRegistry.Application.Abstractions;
using HeroRegistry.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery and HTTP resilience.
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Controllers: JSON in camelCase (default) with enums as strings; model binding errors in the same
// problem-details shape as use-case validation errors (T11).
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ProblemDetailsSetup.InvalidModelStateResponse);

// Same JSON settings for minimal endpoints and the OpenAPI document.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Every error as RFC 9457 problem details, including unhandled exceptions (T11).
builder.Services.AddApiProblemDetails();

// JWT Bearer authentication, the AdminOnly policy, and "signed in" as the default for every endpoint (T09).
builder.Services.AddJwtAuthentication();

// Only the configured UI origins may call the API from a browser (Cors:AllowedOrigins, T11).
builder.Services.AddUiCors(builder.Configuration);

// The signed-in user for the use cases (Application's ICurrentUser port).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// OpenAPI document + Scalar API reference (T02.1), with the JWT Bearer scheme.
builder.Services.AddApiDocumentation();

var app = builder.Build();

// Unhandled exceptions become 500 problem details (no details, privacy-safe logging), and empty-body
// error responses (404, 405, ...) get a problem-details body.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Development only: /openapi/v1.json, /scalar, and "/" redirecting to /scalar (anonymous).
app.MapApiDocumentation();

app.UseHttpsRedirection();

app.UseCors(CorsSettings.PolicyName);

app.UseAuthentication();
app.UseUserTelemetry();
app.UseAuthorization();

app.MapControllers();

// /health (readiness, includes SQL Server) and /alive (liveness), anonymous, in every environment.
app.MapDefaultEndpoints();

// Minimal API: versioned route group -> /api/v1/...
var v1 = app.MapGroup("/api/v1");

v1.MapGet("/test", () => TypedResults.Ok(new
    {
        Message = "Hero Registry API is up and running!",
        Version = "v1",
        ServerTimeUtc = DateTime.UtcNow
    }))
    .WithName("Test")
    .AllowAnonymous();

await app.RunAsync();

/// <summary>
/// Exposed so integration tests can host the API with <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
