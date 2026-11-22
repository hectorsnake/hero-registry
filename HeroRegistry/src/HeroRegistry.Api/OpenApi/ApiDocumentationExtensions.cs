using Scalar.AspNetCore;

namespace HeroRegistry.Api.OpenApi;

/// <summary>
/// OpenAPI document (<c>/openapi/v1.json</c>) and Scalar API reference (<c>/scalar</c>).
/// Both are published in Development only: outside it they would advertise the attack surface.
/// </summary>
public static class ApiDocumentationExtensions
{
    public const string DocumentName = "v1";
    public const string ScalarPath = "/scalar";

    private const string Title = "Hero Registry API";
    private const string Description =
        "REST API to register, search, update and retire superheroes, and to manage user accounts. " +
        "Built with ASP.NET Core and Clean Architecture.";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = Title;
                document.Info.Version = DocumentName;
                document.Info.Description = Description;
                return Task.CompletedTask;
            });

            // JWT bearer: declare the scheme and require it on every non-anonymous endpoint.
            options.AddDocumentTransformer(BearerSecurityTransformers.AddBearerScheme);
            options.AddOperationTransformer(BearerSecurityTransformers.RequireBearerUnlessAnonymous);
        });

        return services;
    }

    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        // The API reference stays reachable without a token (the fallback policy requires one otherwise).
        app.MapOpenApi().AllowAnonymous();

        app.MapScalarApiReference(options => options
            .WithTitle(Title)
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            // Pre-select the Bearer scheme in Scalar's Authentication panel.
            .AddPreferredSecuritySchemes(BearerSecurityTransformers.SchemeName))
            .AllowAnonymous();

        // The API's base URL opens the reference (Aspire dashboard links, browsers, F5).
        app.MapGet("/", () => Results.Redirect(ScalarPath))
            .ExcludeFromDescription()
            .AllowAnonymous();

        return app;
    }
}
