using System.Text.Json;
using HeroRegistry.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HeroRegistry.Api.Controllers;

/// <summary>
/// Base for the API controllers: turns use-case <see cref="Error"/>s into RFC 9457 problem details, in
/// one place. Validation errors become <see cref="ValidationProblemDetails"/> with an <c>errors</c>
/// dictionary; every problem carries the stable <c>code</c> of the error (for example <c>user.duplicate</c>).
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The problem response for an expected failure.</summary>
    protected ActionResult ErrorResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        ProblemDetails problem;
        if (error.FieldErrors.Count > 0)
        {
            // Field names in camelCase, like the JSON properties of the requests.
            var fields = new ModelStateDictionary();
            foreach (var (field, messages) in error.FieldErrors)
            {
                foreach (var message in messages)
                {
                    fields.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(field), message);
                }
            }

            problem = ProblemDetailsFactory.CreateValidationProblemDetails(HttpContext, fields, status, title: error.Message);
        }
        else
        {
            problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title: error.Message);
        }

        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>200 with the value, or the problem response.</summary>
    protected ActionResult<T> OkOrError<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    /// <summary>204, or the problem response.</summary>
    protected ActionResult NoContentOrError(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }
}
