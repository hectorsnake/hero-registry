namespace HeroRegistry.Application.Common;

/// <summary>
/// A unique value (for example a hero name or an email) is already used by another active record.
/// Raised by the repositories when the database's unique indexes reject a write, the safety net
/// behind the use cases' own uniqueness checks (for example two simultaneous requests). The API maps
/// it to 409 Conflict.
/// </summary>
/// <remarks>
/// It deliberately carries no inner exception and no value: SQL Server's message contains the
/// duplicate value (personal data such as an email), which must not reach logs or telemetry.
/// </remarks>
public sealed class DuplicateEntityException(string entityName, string? field)
    : Exception(field is null
        ? $"A {entityName} with the same unique value already exists."
        : $"A {entityName} with the same {field} already exists.")
{
    /// <summary>For example <c>SuperHero</c> or <c>User</c>.</summary>
    public string EntityName { get; } = entityName;

    /// <summary>The property whose value is taken, for example <c>HeroName</c>; null if unknown.</summary>
    public string? Field { get; } = field;
}
