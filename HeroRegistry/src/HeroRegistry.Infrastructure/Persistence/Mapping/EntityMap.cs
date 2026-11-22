using System.Linq.Expressions;
using System.Reflection;

namespace HeroRegistry.Infrastructure.Persistence.Mapping;

/// <summary>
/// Fluent mapping of an entity to a table, configured in Infrastructure, so the domain entities stay
/// free of persistence attributes (T04). Conventions:
/// <list type="bullet">
///   <item>Every public instance property with a setter (of any accessibility) is a column with the same name.</item>
///   <item>Get-only properties are not mapped; other properties can be excluded with <see cref="Ignore{TProperty}"/>.</item>
///   <item>Strings are NVARCHAR(MAX) unless <see cref="ColumnBuilder.HasMaxLength"/> or <see cref="ColumnBuilder.IsAnsi"/> say otherwise.</item>
/// </list>
/// Subclasses configure the map in their constructor. <see cref="EntityMetadata.For{T}"/> turns it into
/// cached metadata.
/// </summary>
public abstract class EntityMap<T>
    where T : class
{
    private readonly Dictionary<string, ColumnBuilder> _columns = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ignored = new(StringComparer.Ordinal);
    private readonly List<string> _softDeleteAlsoSets = [];

    internal string? TableName { get; private set; }

    internal string Schema { get; private set; } = "dbo";

    internal string? KeyProperty { get; private set; }

    internal string? SoftDeleteProperty { get; private set; }

    internal IReadOnlyList<string> SoftDeleteAlsoSets => _softDeleteAlsoSets;

    internal IReadOnlySet<string> IgnoredProperties => _ignored;

    internal ColumnBuilder? FindColumn(string propertyName) => _columns.GetValueOrDefault(propertyName);

    protected void ToTable(string name, string schema = "dbo")
    {
        TableName = name;
        Schema = schema;
    }

    /// <summary>The identity primary key: never inserted or updated, returned by INSERT.</summary>
    protected void HasKey<TProperty>(Expression<Func<T, TProperty>> key) => KeyProperty = PropertyName(key);

    protected ColumnBuilder Property<TProperty>(Expression<Func<T, TProperty>> property)
    {
        var name = PropertyName(property);
        if (!_columns.TryGetValue(name, out var builder))
        {
            builder = new ColumnBuilder();
            _columns[name] = builder;
        }

        return builder;
    }

    protected void Ignore<TProperty>(Expression<Func<T, TProperty>> property) => _ignored.Add(PropertyName(property));

    /// <summary>
    /// Rows are never physically deleted: reads skip rows where <paramref name="flag"/> is true, and a
    /// soft delete sets it together with <paramref name="alsoSet"/> (for example the update audit).
    /// </summary>
    protected void HasSoftDelete(Expression<Func<T, bool>> flag, params Expression<Func<T, object?>>[] alsoSet)
    {
        SoftDeleteProperty = PropertyName(flag);
        _softDeleteAlsoSets.Clear();
        _softDeleteAlsoSets.AddRange(alsoSet.Select(PropertyName));
    }

    /// <summary>Accepts only <c>entity => entity.Property</c>.</summary>
    private static string PropertyName(LambdaExpression expression)
    {
        var body = expression.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert
            ? convert.Operand
            : expression.Body;

        if (body is MemberExpression { Member: PropertyInfo property, Expression: ParameterExpression })
        {
            return property.Name;
        }

        throw new ArgumentException(
            $"'{expression}' must select a property of {typeof(T).Name}, for example x => x.Name.",
            nameof(expression));
    }
}

/// <summary>Column options for one property.</summary>
public sealed class ColumnBuilder
{
    internal string? ColumnName { get; private set; }

    internal int? MaxLength { get; private set; }

    internal bool Ansi { get; private set; }

    internal bool FixedLength { get; private set; }

    internal bool Json { get; private set; }

    internal bool InsertOnly { get; private set; }

    public ColumnBuilder HasColumnName(string name)
    {
        ColumnName = name;
        return this;
    }

    /// <summary>The string column size (NVARCHAR(n)). Default: MAX.</summary>
    public ColumnBuilder HasMaxLength(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        MaxLength = maxLength;
        return this;
    }

    /// <summary>A non-Unicode string column (VARCHAR, or CHAR with <see cref="IsFixedLength"/>).</summary>
    public ColumnBuilder IsAnsi()
    {
        Ansi = true;
        return this;
    }

    /// <summary>A fixed-length string column (NCHAR or CHAR).</summary>
    public ColumnBuilder IsFixedLength()
    {
        FixedLength = true;
        return this;
    }

    /// <summary>The value is serialized with System.Text.Json into NVARCHAR(MAX), for example a list of strings.</summary>
    public ColumnBuilder StoredAsJson()
    {
        Json = true;
        return this;
    }

    /// <summary>Written by INSERT, never changed by UPDATE (for example the creation audit).</summary>
    public ColumnBuilder IsInsertOnly()
    {
        InsertOnly = true;
        return this;
    }
}
