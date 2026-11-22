using HeroRegistry.Application;
using HeroRegistry.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddAuthorization();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

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
