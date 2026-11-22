using HeroRegistry.Domain.Heroes;

namespace HeroRegistry.Application.Heroes;

/// <summary>Hand-written mapping between the hero DTOs and the domain entity (no AutoMapper needed).</summary>
internal static class HeroMapping
{
    public static HeroResponse ToResponse(this SuperHero hero) => new(
        hero.Id,
        hero.HeroName,
        hero.FirstName,
        hero.LastName,
        hero.BirthDate,
        hero.Email,
        hero.Superpowers,
        hero.PowerLevel,
        hero.Address1,
        hero.Address2,
        hero.City,
        hero.State,
        hero.ZipCode,
        hero.CreatedBy,
        hero.CreatedAt,
        hero.UpdatedBy,
        hero.UpdatedAt);

    /// <summary>
    /// Call only after <see cref="HeroRequestValidator"/> succeeded: required values are then present.
    /// The domain entity trims and normalizes them.
    /// </summary>
    public static SuperHeroDetails ToDetails(this HeroRequest request) => new(
        request.HeroName!,
        request.FirstName!,
        request.LastName!,
        request.BirthDate!.Value,
        request.Email!,
        request.Superpowers!,
        request.PowerLevel!.Value,
        request.Address1!,
        request.Address2,
        request.City!,
        request.State!,
        request.ZipCode!);
}
