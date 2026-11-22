namespace HeroRegistry.Infrastructure.Persistence.Diagnostics;

/// <summary>The kinds of database work the data layer performs.</summary>
public enum DataOperation
{
    Select,
    Insert,
    Update,

    /// <summary>An UPDATE that sets <c>IsDeleted = 1</c>. Rows are never physically deleted.</summary>
    SoftDelete,
}

internal static class DataOperationExtensions
{
    /// <summary>Name used in span names and log messages, for example "INSERT SuperHeroes".</summary>
    public static string DisplayName(this DataOperation operation) => operation switch
    {
        DataOperation.Select => "SELECT",
        DataOperation.Insert => "INSERT",
        DataOperation.Update => "UPDATE",
        DataOperation.SoftDelete => "SOFT DELETE",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };

    /// <summary>The SQL statement actually executed (OpenTelemetry <c>db.operation.name</c>).</summary>
    public static string SqlStatement(this DataOperation operation) => operation switch
    {
        DataOperation.Select => "SELECT",
        DataOperation.Insert => "INSERT",
        DataOperation.Update or DataOperation.SoftDelete => "UPDATE",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };
}
