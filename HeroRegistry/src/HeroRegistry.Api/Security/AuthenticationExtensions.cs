using System.Diagnostics;
using System.Globalization;
using System.Text;
using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HeroRegistry.Api.Security;

/// <summary>JWT Bearer authentication and the authorization policies of the API.</summary>
public static class AuthenticationExtensions
{
    /// <summary>Policy for administrator-only endpoints (the <c>Admin</c> role).</summary>
    public const string AdminOnlyPolicy = "AdminOnly";

    /// <summary>
    /// Validates the tokens issued by <see cref="JwtTokenService"/> with the same <see cref="JwtOptions"/>
    /// (validated at startup by Infrastructure). Every endpoint requires an authenticated user unless it
    /// opts out with <c>[AllowAnonymous]</c> / <c>AllowAnonymous()</c> (fallback policy).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the claim names the tokens use ("sub", "unique_name", "role") instead of the
                // legacy WS-* URIs, so HttpCurrentUser and IsInRole(Roles.Admin) read them as issued.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role",
                };

                // 401 and 403 from authentication and authorization as problem details, like every other
                // error (by default they have an empty body).
                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
                            ? JwtBearerDefaults.AuthenticationScheme
                            : $"{JwtBearerDefaults.AuthenticationScheme} error=\"invalid_token\"";

                        await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                            context.AuthenticateFailure is null ? "You must be signed in." : "The access token is invalid or has expired.",
                            "auth.unauthenticated");
                    },
                    OnForbidden = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                        "You do not have permission to do that.", "auth.forbidden"),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminOnlyPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin))
            // Secure by default: an endpoint without attributes still requires a signed-in user.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string title, string code)
    {
        httpContext.Response.StatusCode = status;
        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["code"] = code },
            },
        });
    }

    /// <summary>
    /// Tags the request's telemetry with the signed-in user's id (<c>enduser.id</c>, T02.2), never the
    /// user name or email, so Application Insights can filter requests by user. Call after UseAuthentication.
    /// </summary>
    public static IApplicationBuilder UseUserTelemetry(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var userId = context.RequestServices.GetRequiredService<ICurrentUser>().UserId;
            if (userId is not null)
            {
                Activity.Current?.SetTag("enduser.id", userId.Value.ToString(CultureInfo.InvariantCulture));
            }

            await next(context);
        });
}
