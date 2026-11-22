namespace HeroRegistry.Application.Heroes;

/// <summary>
/// The editable hero fields, as sent by a client. Everything is nullable so a missing field becomes a
/// validation error ("HeroName is required.") instead of a binding failure.
/// </summary>
public abstract record HeroRequest
{
    public string? HeroName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public DateOnly? BirthDate { get; init; }

    public string? Email { get; init; }

    public IReadOnlyList<string>? Superpowers { get; init; }

    public int? PowerLevel { get; init; }

    public string? Address1 { get; init; }

    public string? Address2 { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? ZipCode { get; init; }
}

/// <summary>Body of <c>POST /api/heroes</c>.</summary>
public sealed record CreateHeroRequest : HeroRequest;

/// <summary>Body of <c>PUT /api/heroes/{id}</c>: replaces every editable field.</summary>
public sealed record UpdateHeroRequest : HeroRequest;

/// <summary>A hero as returned by the API, including the audit trail.</summary>
public sealed record HeroResponse(
    int Id,
    string HeroName,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Email,
    IReadOnlyList<string> Superpowers,
    int PowerLevel,
    string Address1,
    string? Address2,
    string City,
    string State,
    string ZipCode,
    int? CreatedBy,
    DateTime CreatedAt,
    int? UpdatedBy,
    DateTime? UpdatedAt);
