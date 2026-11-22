using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace HeroRegistry.Api.ErrorHandling;

/// <summary>
/// The last line of defense for exceptions that escape the use cases (which return expected failures as
/// <see cref="Result"/> errors, not exceptions):
/// <list type="bullet">
///   <item><see cref="DomainException"/> becomes 400 on its field, and <see cref="DuplicateEntityException"/> becomes 409.</item>
///   <item>Anything else becomes a 500 problem with a generic title: no message, type or stack trace in the response.</item>
///   <item>Client disconnects become 499 without a body or an error log.</item>
/// </list>
/// Logging and telemetry respect the privacy rule of T02.2: database exception messages can echo data
/// ("The duplicate key value is (someone@example.com)"), so for those only the type and SQL error number
/// are recorded. Code bugs keep their full exception for diagnosis.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(logger, httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        ProblemDetails problem = exception switch
        {
            DomainException domain => Validation(domain),
            DuplicateEntityException duplicate => Duplicate(duplicate),
            _ => Unexpected(httpContext, exception),
        };

        httpContext.Response.StatusCode = problem.Status!.Value;

        // The exception is deliberately not passed to the writer, so no detail can leak into the body.
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private static HttpValidationProblemDetails Validation(DomainException exception) =>
        new(new Dictionary<string, string[]> { [CamelCase(exception.Field)] = [exception.Message] })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more fields are invalid.",
            Extensions = { ["code"] = "validation.failed" },
        };

    private static ProblemDetails Duplicate(DuplicateEntityException exception)
    {
        ProblemDetails problem = exception.Field is null
            ? new ProblemDetails()
            : new HttpValidationProblemDetails(new Dictionary<string, string[]>
            {
                [CamelCase(exception.Field)] = [exception.Message],
            });

        problem.Status = StatusCodes.Status409Conflict;
        problem.Title = exception.Message;
        problem.Extensions["code"] = "duplicate";
        return problem;
    }

    private ProblemDetails Unexpected(HttpContext httpContext, Exception exception)
    {
        var request = httpContext.Request;
        var databaseError = FindDatabaseException(exception);

        if (databaseError is null)
        {
            LogUnhandled(logger, exception, exception.GetType().FullName!, request.Method, request.Path);
            Activity.Current?.AddException(exception);
        }
        else
        {
            var type = databaseError.GetType().FullName!;
            var sqlErrorNumber = (databaseError as SqlException)?.Number;
            LogUnhandledDatabase(logger, type, sqlErrorNumber, request.Method, request.Path);
            Activity.Current?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = type,
            }));
        }

        Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Extensions = { ["code"] = "server.error" },
        };
    }

    /// <summary>The first database exception in the chain (the exception itself or an inner one).</summary>
    private static DbException? FindDatabaseException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException db)
            {
                return db;
            }
        }

        return null;
    }

    private static string CamelCase(string field) => JsonNamingPolicy.CamelCase.ConvertName(field);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Error,
        Message = "Unhandled {ExceptionType} while processing {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string exceptionType, string method, string path);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Error,
        Message = "Unhandled {ExceptionType} (SQL error {SqlErrorNumber}) while processing {Method} {Path}; the message is withheld because it can contain personal data")]
    private static partial void LogUnhandledDatabase(ILogger logger, string exceptionType, int? sqlErrorNumber, string method, string path);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Debug,
        Message = "Request {Method} {Path} was aborted by the client")]
    private static partial void LogRequestAborted(ILogger logger, string method, string path);
}
