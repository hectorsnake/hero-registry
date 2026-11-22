using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HeroRegistry.Infrastructure.Persistence.Mapping;

/// <summary>
/// The table, key and columns of an entity, read once with Reflection from its <see cref="EntityMap{T}"/>
/// and cached per map type. Table and column names come only from here, and are validated as plain
/// identifiers, so they are safe to put into SQL text. Values always go through parameters.
/// </summary>
public sealed partial class EntityMetadata
{
    private static readonly ConcurrentDictionary<Type, EntityMetadata> Cache = new();

    private readonly Dictionary<string, ColumnMetadata> _columnsByName;
    private readonly ConstructorInfo _constructor;

    private EntityMetadata(
        Type entityType,
        string schema,
        string tableName,
        IReadOnlyList<ColumnMetadata> columns,
        ColumnMetadata key,
        ColumnMetadata? softDeleteFlag,
        IReadOnlyList<ColumnMetadata> softDeleteAlsoSets,
        ConstructorInfo constructor)
    {
        EntityType = entityType;
        TableName = tableName;
        QualifiedTableName = $"{Quote(schema)}.{Quote(tableName)}";
        Columns = columns;
        Key = key;
        SoftDeleteFlag = softDeleteFlag;
        SoftDeleteAlsoSets = softDeleteAlsoSets;
        _constructor = constructor;
        _columnsByName = columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Type EntityType { get; }

    /// <summary>The unquoted table name, for example <c>SuperHeroes</c> (used in logs and spans).</summary>
    public string TableName { get; }

    /// <summary>The quoted table name for SQL, for example <c>[dbo].[SuperHeroes]</c>.</summary>
    public string QualifiedTableName { get; }

    /// <summary>Mapped columns, the key first, then the entity's properties in reflection order.</summary>
    public IReadOnlyList<ColumnMetadata> Columns { get; }

    public ColumnMetadata Key { get; }

    /// <summary>The soft-delete flag column, or <see langword="null"/> when rows are physically deleted.</summary>
    public ColumnMetadata? SoftDeleteFlag { get; }

    /// <summary>Columns a soft delete sets together with the flag (for example UpdatedBy and UpdatedAt).</summary>
    public IReadOnlyList<ColumnMetadata> SoftDeleteAlsoSets { get; }

    /// <summary>Cached metadata for the map's entity. Building it validates the map.</summary>
    public static EntityMetadata For<T>(EntityMap<T> map)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(map);
        return Cache.GetOrAdd(map.GetType(), static (_, m) => Build(m), map);
    }

    /// <summary>Finds a column by its column name, ignoring case.</summary>
    public ColumnMetadata? FindColumn(string columnName) => _columnsByName.GetValueOrDefault(columnName);

    /// <summary>Creates an empty entity through its (usually private) parameterless constructor.</summary>
    internal object CreateInstance() => _constructor.Invoke(null);

    /// <summary>Brackets around a validated identifier: <c>Name</c> becomes <c>[Name]</c>.</summary>
    internal static string Quote(string identifier) => $"[{identifier}]";

    private static EntityMetadata Build<T>(EntityMap<T> map)
        where T : class
    {
        var type = typeof(T);

        var tableName = map.TableName ?? throw new InvalidOperationException(
            $"{map.GetType().Name} must call ToTable(...) to name the table for {type.Name}.");
        ValidateIdentifier(tableName, "table");
        ValidateIdentifier(map.Schema, "schema");

        var keyProperty = map.KeyProperty ?? throw new InvalidOperationException(
            $"{map.GetType().Name} must call HasKey(...) to define the identity key of {type.Name}.");

        var constructor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"{type.Name} needs a parameterless constructor (it can be private) to be loaded from the database.");

        var nullability = new NullabilityInfoContext();

        var columns = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.GetSetMethod(nonPublic: true) is not null) // get-only properties are computed
            .Where(p => !map.IgnoredProperties.Contains(p.Name))
            .Select(p => CreateColumn(p, map.FindColumn(p.Name), p.Name == keyProperty, p.Name == map.SoftDeleteProperty, nullability))
            .OrderByDescending(c => c.IsKey) // key first, the rest in reflection order
            .ToList();

        var key = columns.SingleOrDefault(c => c.IsKey) ?? throw new InvalidOperationException(
            $"The key property '{keyProperty}' of {type.Name} is not a mapped property with a setter.");

        ColumnMetadata? softDeleteFlag = null;
        List<ColumnMetadata> softDeleteAlsoSets = [];
        if (map.SoftDeleteProperty is not null)
        {
            softDeleteFlag = columns.Single(c => c.Property.Name == map.SoftDeleteProperty);
            softDeleteAlsoSets = map.SoftDeleteAlsoSets
                .Select(name => columns.SingleOrDefault(c => c.Property.Name == name) ?? throw new InvalidOperationException(
                    $"Soft delete column '{name}' of {type.Name} is not mapped."))
                .ToList();
        }

        return new EntityMetadata(type, map.Schema, tableName, columns, key, softDeleteFlag, softDeleteAlsoSets, constructor);
    }

    private static ColumnMetadata CreateColumn(
        PropertyInfo property,
        ColumnBuilder? options,
        bool isKey,
        bool isSoftDeleteFlag,
        NullabilityInfoContext nullability)
    {
        var name = options?.ColumnName ?? property.Name;
        ValidateIdentifier(name, "column");

        var isJson = options?.Json ?? false;
        var storageType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var (dbType, size) = isJson
            ? (DbType.String, -1)
            : ResolveDbType(storageType, options, property);

        var isNullable = property.PropertyType.IsValueType
            ? Nullable.GetUnderlyingType(property.PropertyType) is not null
            : nullability.Create(property).WriteState != NullabilityState.NotNull;

        return new ColumnMetadata(
            name,
            property,
            property.GetSetMethod(nonPublic: true)!,
            storageType,
            dbType,
            size,
            isKey,
            isJson,
            options?.InsertOnly ?? false,
            isSoftDeleteFlag,
            isNullable);
    }

    private static (DbType DbType, int Size) ResolveDbType(Type type, ColumnBuilder? options, PropertyInfo property)
    {
        if (type == typeof(string))
        {
            var ansi = options?.Ansi ?? false;
            var fixedLength = options?.FixedLength ?? false;
            var dbType = (ansi, fixedLength) switch
            {
                (true, true) => DbType.AnsiStringFixedLength,
                (true, false) => DbType.AnsiString,
                (false, true) => DbType.StringFixedLength,
                (false, false) => DbType.String,
            };
            return (dbType, options?.MaxLength ?? -1);
        }

        if (type.IsEnum)
        {
            type = Enum.GetUnderlyingType(type);
        }

        var mapped = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => DbType.Boolean,
            TypeCode.Byte => DbType.Byte,
            TypeCode.Int16 => DbType.Int16,
            TypeCode.Int32 => DbType.Int32,
            TypeCode.Int64 => DbType.Int64,
            TypeCode.Decimal => DbType.Decimal,
            TypeCode.Double => DbType.Double,
            TypeCode.Single => DbType.Single,
            TypeCode.DateTime => DbType.DateTime2,
            _ when type == typeof(DateOnly) => DbType.Date,
            _ when type == typeof(DateTimeOffset) => DbType.DateTimeOffset,
            _ when type == typeof(Guid) => DbType.Guid,
            _ => throw new NotSupportedException(
                $"{property.DeclaringType?.Name}.{property.Name} has type {property.PropertyType.Name}, which the " +
                "mapper does not support. Ignore it, or store it as JSON with StoredAsJson()."),
        };

        return (mapped, 0);
    }

    private static void ValidateIdentifier(string identifier, string kind)
    {
        if (!IdentifierFormat().IsMatch(identifier))
        {
            throw new ArgumentException(
                $"'{identifier}' is not a valid {kind} name: use letters, digits and underscores, starting with a letter or underscore.",
                nameof(identifier));
        }
    }

    // A plain SQL identifier: nothing that could end the brackets or start another statement.
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierFormat();
}

/// <summary>One mapped column: its property, database type and role in the generated statements.</summary>
public sealed class ColumnMetadata
{
    private readonly MethodInfo _setter;

    internal ColumnMetadata(
        string name,
        PropertyInfo property,
        MethodInfo setter,
        Type storageType,
        DbType dbType,
        int size,
        bool isKey,
        bool isJson,
        bool isInsertOnly,
        bool isSoftDeleteFlag,
        bool isNullable)
    {
        Name = name;
        QuotedName = EntityMetadata.Quote(name);
        ParameterName = $"@{name}";
        Property = property;
        _setter = setter;
        StorageType = storageType;
        DbType = dbType;
        Size = size;
        IsKey = isKey;
        IsJson = isJson;
        IsInsertOnly = isInsertOnly;
        IsSoftDeleteFlag = isSoftDeleteFlag;
        IsNullable = isNullable;
    }

    /// <summary>The column name, for example <c>HeroName</c>.</summary>
    public string Name { get; }

    /// <summary>The column name for SQL, for example <c>[HeroName]</c>.</summary>
    public string QuotedName { get; }

    /// <summary>The default parameter name, for example <c>@HeroName</c>.</summary>
    public string ParameterName { get; }

    public PropertyInfo Property { get; }

    /// <summary>The property type without <see cref="Nullable{T}"/>.</summary>
    public Type StorageType { get; }

    public DbType DbType { get; }

    /// <summary>String size: the maximum length, -1 for MAX, 0 for non-string types.</summary>
    public int Size { get; }

    public bool IsKey { get; }

    public bool IsJson { get; }

    public bool IsInsertOnly { get; }

    public bool IsSoftDeleteFlag { get; }

    /// <summary>Whether the property accepts null (<c>int?</c>, <c>string?</c>).</summary>
    public bool IsNullable { get; }

    internal object? GetValue(object entity) => Property.GetValue(entity);

    /// <summary>Calls the (usually private) setter through Reflection.</summary>
    internal void SetValue(object entity, object? value) => _setter.Invoke(entity, [value]);
}
