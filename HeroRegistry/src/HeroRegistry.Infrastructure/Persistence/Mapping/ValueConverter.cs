using System.Globalization;
using System.Text.Json;

namespace HeroRegistry.Infrastructure.Persistence.Mapping;

/// <summary>Converts property values to and from what ADO.NET reads and writes.</summary>
internal static class ValueConverter
{
    /// <summary>Property value to parameter value. <see langword="null"/> means SQL NULL.</summary>
    public static object? ToDatabase(ColumnMetadata column, object? value) => value switch
    {
        null => null,
        _ when column.IsJson => JsonSerializer.Serialize(value, value.GetType()),
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        Enum => System.Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture),
        _ => value,
    };

    /// <summary>Column value (from a data reader) to property value.</summary>
    /// <exception cref="InvalidOperationException">The value does not fit the property.</exception>
    public static object? FromDatabase(ColumnMetadata column, object? value)
    {
        if (value is null or DBNull)
        {
            return column.IsNullable
                ? null
                : throw new InvalidOperationException(
                    $"Column '{column.Name}' is NULL, but {column.Property.DeclaringType?.Name}.{column.Property.Name} does not accept null.");
        }

        try
        {
            return Convert(column, value);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or JsonException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"Column '{column.Name}' value of type {value.GetType().Name} cannot be converted to " +
                $"{column.Property.PropertyType.Name} ({column.Property.DeclaringType?.Name}.{column.Property.Name}).",
                ex);
        }
    }

    private static object? Convert(ColumnMetadata column, object value)
    {
        var target = column.StorageType;

        if (column.IsJson)
        {
            return JsonSerializer.Deserialize((string)value, JsonTargetType(column.Property.PropertyType));
        }

        if (target == typeof(DateOnly))
        {
            return value is DateTime dateTime ? DateOnly.FromDateTime(dateTime) : (DateOnly)value;
        }

        if (target == typeof(DateTime))
        {
            // DATETIME2 carries no time zone: the application always stores UTC.
            return value is DateTimeOffset offset
                ? offset.UtcDateTime
                : DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
        }

        if (target.IsEnum)
        {
            return value is string name
                ? Enum.Parse(target, name, ignoreCase: true)
                : Enum.ToObject(target, value);
        }

        return target.IsInstanceOfType(value)
            ? value
            : System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// For interface collection types (<c>IReadOnlyList&lt;string&gt;</c>, <c>IEnumerable&lt;string&gt;</c>, ...)
    /// deserialize into an array, which implements them all.
    /// </summary>
    private static Type JsonTargetType(Type propertyType)
    {
        if (propertyType.IsInterface && propertyType.IsGenericType)
        {
            var definition = propertyType.GetGenericTypeDefinition();
            if (definition == typeof(IEnumerable<>)
                || definition == typeof(IReadOnlyCollection<>)
                || definition == typeof(IReadOnlyList<>)
                || definition == typeof(ICollection<>)
                || definition == typeof(IList<>))
            {
                return propertyType.GenericTypeArguments[0].MakeArrayType();
            }
        }

        return propertyType;
    }
}
