using HeroRegistry.Api.OpenApi;
using HeroRegistry.Application;
using HeroRegistry.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery and HTTP resilience.
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddAuthorization();

// OpenAPI document + Scalar API reference (T02.1).
builder.Services.AddApiDocumentation();

var app = builder.Build();

// Development only: /openapi/v1.json, /scalar, and "/" redirecting to /scalar.
app.MapApiDocumentation();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// /health (readiness, includes SQL Server) and /alive (liveness).
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
