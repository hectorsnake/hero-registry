using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HeroRegistry.Api.OpenApi;

/// <summary>
/// Describes the JWT bearer authentication in the OpenAPI document, so Scalar shows an
/// Authentication panel and sends <c>Authorization: Bearer &lt;token&gt;</c> on protected endpoints.
/// </summary>
internal static class BearerSecurityTransformers
{
    public const string SchemeName = "Bearer";

    /// <summary>Declares the <c>Bearer</c> security scheme under <c>components.securitySchemes</c>.</summary>
    public static Task AddBearerScheme(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the accessToken returned by POST /api/auth/login (without the 'Bearer ' prefix).",
        };

        return Task.CompletedTask;
    }

    /// <summary>
    /// Marks every operation as requiring the bearer token, except the ones that opt out with
    /// <c>[AllowAnonymous]</c> / <c>AllowAnonymous()</c> (the API's fallback policy requires a signed-in user).
    /// </summary>
    public static Task RequireBearerUnlessAnonymous(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var isAnonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (isAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
        });

        return Task.CompletedTask;
    }
}
