using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HeroRegistry.Api.ErrorHandling;

/// <summary>
/// One error format for the whole API: RFC 9457 problem details with <c>traceId</c> (added by ASP.NET
/// Core; the trace id is Application Insights' <c>operation_Id</c>) and a stable <c>code</c>.
/// </summary>
public static class ProblemDetailsSetup
{
    public const string ValidationTitle = "One or more fields are invalid.";

    /// <summary>
    /// Problem details for every error: use-case errors (ApiControllerBase), unhandled exceptions
    /// (<see cref="GlobalExceptionHandler"/>), empty-body status codes (status code pages), and
    /// authentication challenges (JWT events).
    /// </summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                // A default code when the producer did not set one (404, 405, 415, ...).
                context.ProblemDetails.Extensions.TryAdd("code", DefaultCode(context.ProblemDetails.Status)));

        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Model binding and JSON errors (handled by [ApiController] before the action runs) in the same
    /// shape as use-case validation errors: camelCase field names, readable messages, and no parser
    /// internals (type names, line numbers).
    /// </summary>
    public static IActionResult InvalidModelStateResponse(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errors = new Dictionary<string, List<string>>();
        foreach (var (key, entry) in context.ModelState)
        {
            foreach (var error in entry.Errors)
            {
                var field = FieldName(key);
                var message = Message(field, error.ErrorMessage);
                if (!errors.TryGetValue(field, out var messages))
                {
                    errors[field] = messages = [];
                }

                if (!messages.Contains(message))
                {
                    messages.Add(message);
                }
            }
        }

        // A failed deserialization also reports "the request body is required": drop that noise when a
        // more precise error exists (a specific field, or "not valid JSON").
        if (errors.TryGetValue("body", out var body))
        {
            if (errors.Count > 1)
            {
                errors.Remove("body");
            }
            else if (body.Count > 1)
            {
                body.RemoveAll(m => m == "A request body is required.");
            }
        }

        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            messages.ForEach(message => modelState.AddModelError(field, message));
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(
            context.HttpContext, modelState, StatusCodes.Status400BadRequest, title: ValidationTitle);
        problem.Extensions["code"] = "validation.failed";

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string DefaultCode(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "validation.failed",
        StatusCodes.Status401Unauthorized => "auth.unauthenticated",
        StatusCodes.Status403Forbidden => "auth.forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        >= 500 => "server.error",
        _ => "error",
    };

    /// <summary><c>$.birthDate</c> becomes <c>birthDate</c>, <c>SortBy</c> becomes <c>sortBy</c>, and the body itself becomes <c>body</c>.</summary>
    private static string FieldName(string key)
    {
        if (string.IsNullOrEmpty(key) || key == "$" || key.Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }

        var path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }

    private static string Message(string field, string message)
    {
        if (field == "body")
        {
            return message.Contains("required", StringComparison.OrdinalIgnoreCase)
                ? "A request body is required."
                : "The request body is not valid JSON.";
        }

        // System.Text.Json messages mention .NET types and positions: replace them.
        var isParserMessage = string.IsNullOrWhiteSpace(message)
            || message.StartsWith("The JSON value", StringComparison.Ordinal)
            || message.Contains("LineNumber", StringComparison.Ordinal)
            || message == "The input was not valid.";

        return isParserMessage ? $"{field} has an invalid value." : message;
    }
}
