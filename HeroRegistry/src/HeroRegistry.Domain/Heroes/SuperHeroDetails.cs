namespace HeroRegistry.Domain.Heroes;

/// <summary>
/// The editable details of a superhero, as received from the outside (for example an API request).
/// They are validated and normalized by <see cref="SuperHero.Create"/> and <see cref="SuperHero.Update"/>.
/// </summary>
public sealed record SuperHeroDetails(
    string HeroName,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Email,
    IReadOnlyCollection<string> Superpowers,
    int PowerLevel,
    string Address1,
    string? Address2,
    string City,
    string State,
    string ZipCode);
