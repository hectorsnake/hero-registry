namespace HeroRegistry.Domain.Common;

/// <summary>Shared validation helpers. Each failure is a <see cref="DomainException"/> naming the field.</summary>
internal static class Guard
{
    /// <summary>Trims the value and checks it is present and within the length limits.</summary>
    public static string RequiredText(string? value, string field, int minLength, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException(field, $"{field} is required.");
        }

        if (trimmed.Length < minLength || trimmed.Length > maxLength)
        {
            throw new DomainException(field, $"{field} must be between {minLength} and {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>Trims the value. A blank value becomes <see langword="null"/>.</summary>
    public static string? OptionalText(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(field, $"{field} cannot be longer than {maxLength} characters.");
        }

        return trimmed;
    }

    public static void ValidUserId(int userId, string field)
    {
        if (userId <= 0)
        {
            throw new DomainException(field, $"{field} must be a valid user id.");
        }
    }

    public static void Utc(DateTime value, string field)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(field, $"{field} must be a UTC time.");
        }
    }
}
