using System.Data;

namespace HeroRegistry.Infrastructure.Persistence.Mapping;

/// <summary>
/// Maps data reader rows to entities with Reflection. Column positions are resolved once per reader
/// (by name, ignoring case); then each row only reads values and calls the cached setters.
/// Reader columns that are not mapped (for example a COUNT(*) OVER() total) are ignored, and mapped
/// properties missing from the result keep their default values.
/// </summary>
public sealed class DataRecordMapper<T>
    where T : class
{
    private readonly EntityMetadata _metadata;
    private readonly (ColumnMetadata Column, int Ordinal)[] _bindings;

    /// <param name="metadata">The entity's metadata.</param>
    /// <param name="schema">Any record of the result set (usually the reader itself), to resolve column positions.</param>
    public DataRecordMapper(EntityMetadata metadata, IDataRecord schema)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(schema);

        if (metadata.EntityType != typeof(T))
        {
            throw new ArgumentException(
                $"The metadata describes {metadata.EntityType.Name}, not {typeof(T).Name}.", nameof(metadata));
        }

        _metadata = metadata;

        var bindings = new List<(ColumnMetadata, int)>();
        var seen = new HashSet<ColumnMetadata>();
        for (var ordinal = 0; ordinal < schema.FieldCount; ordinal++)
        {
            var column = metadata.FindColumn(schema.GetName(ordinal));
            if (column is not null && seen.Add(column))
            {
                bindings.Add((column, ordinal));
            }
        }

        _bindings = [.. bindings];
    }

    public T Map(IDataRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = _metadata.CreateInstance();

        foreach (var (column, ordinal) in _bindings)
        {
            var raw = record.IsDBNull(ordinal) ? null : record.GetValue(ordinal);
            column.SetValue(entity, ValueConverter.FromDatabase(column, raw));
        }

        return (T)entity;
    }
}
