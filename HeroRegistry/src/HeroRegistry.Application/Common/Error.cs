namespace HeroRegistry.Application.Common;

/// <summary>The kinds of expected failures. The API maps each one to an HTTP status code.</summary>
public enum ErrorType
{
    /// <summary>400: the input breaks one or more rules (see <see cref="Error.FieldErrors"/>).</summary>
    Validation,

    /// <summary>401: no signed-in user.</summary>
    Unauthorized,

    /// <summary>403: signed in, but not allowed (for example a non-admin deleting a hero).</summary>
    Forbidden,

    /// <summary>404: the record does not exist or was deleted.</summary>
    NotFound,

    /// <summary>409: a unique value (for example a hero name) is already used.</summary>
    Conflict,
}

/// <summary>An expected failure: a type, a stable machine-readable code, a message and optional field errors.</summary>
public sealed record Error(ErrorType Type, string Code, string Message, IReadOnlyDictionary<string, string[]> FieldErrors);

/// <summary>Factory methods for <see cref="Error"/>.</summary>
public static class Errors
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFields = new Dictionary<string, string[]>();

    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(ErrorType.Validation, "validation.failed", "One or more fields are invalid.", fieldErrors);

    public static Error NotFound(string code, string message) =>
        new(ErrorType.NotFound, code, message, NoFields);

    public static Error Conflict(string code, string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(ErrorType.Conflict, code, message, fieldErrors ?? NoFields);

    public static Error Forbidden(string message) =>
        new(ErrorType.Forbidden, "auth.forbidden", message, NoFields);

    public static Error Unauthorized(string message = "You must be signed in.") =>
        new(ErrorType.Unauthorized, "auth.unauthenticated", message, NoFields);
}
