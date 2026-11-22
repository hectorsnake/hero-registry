namespace HeroRegistry.Application.Common;

/// <summary>
/// The outcome of a use case: success, or an expected <see cref="Common.Error"/> (validation, not found,
/// conflict...). Expected failures are values, not exceptions; exceptions are left for the unexpected.
/// </summary>
public class Result
{
    private protected Result(Error? error) => Error = error;

    public bool IsSuccess => Error is null;

    /// <summary>The failure, or <see langword="null"/> on success.</summary>
    public Error? Error { get; }

    public static Result Success() => new(null);

    public static implicit operator Result(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }
}

/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null) => _value = value;

    private Result(Error error)
        : base(error)
    {
    }

    /// <exception cref="InvalidOperationException">The result is a failure; check <see cref="Result.IsSuccess"/> first.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value ({Error!.Code}).");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
}
