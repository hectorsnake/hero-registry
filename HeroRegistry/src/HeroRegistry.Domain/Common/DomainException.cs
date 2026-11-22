namespace HeroRegistry.Domain.Common;

/// <summary>
/// A business rule was violated. <see cref="Field"/> names the offending property, so the API can
/// return field-level validation errors (400 ValidationProblemDetails).
/// </summary>
public sealed class DomainException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
