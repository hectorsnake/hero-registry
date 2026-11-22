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

builder.Services.AddControllers();

// JWT Bearer authentication, the AdminOnly policy, and "signed in" as the default for every endpoint (T09).
builder.Services.AddJwtAuthentication();

// The signed-in user for the use cases (Application's ICurrentUser port).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// OpenAPI document + Scalar API reference (T02.1).
builder.Services.AddApiDocumentation();

var app = builder.Build();

// Development only: /openapi/v1.json, /scalar, and "/" redirecting to /scalar (anonymous).
app.MapApiDocumentation();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseUserTelemetry();
app.UseAuthorization();

app.MapControllers();

// /health (readiness, includes SQL Server) and /alive (liveness), anonymous.
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
