using HeroRegistry.Domain.Heroes;
using HeroRegistry.Infrastructure.Persistence.Mapping;
using static HeroRegistry.Domain.Heroes.SuperHeroRules;

namespace HeroRegistry.Infrastructure.Persistence.Maps;

/// <summary>Maps <see cref="SuperHero"/> to <c>dbo.SuperHeroes</c> (0002_CreateSuperHeroes.sql).</summary>
internal sealed class SuperHeroMap : AuditableEntityMap<SuperHero>
{
    public SuperHeroMap()
    {
        ToTable("SuperHeroes");
        Property(h => h.HeroName).HasMaxLength(HeroNameMaxLength);
        Property(h => h.FirstName).HasMaxLength(PersonNameMaxLength);
        Property(h => h.LastName).HasMaxLength(PersonNameMaxLength);
        Property(h => h.Email).HasMaxLength(Domain.ValueObjects.EmailAddress.MaxLength);
        Property(h => h.Superpowers).StoredAsJson();
        Property(h => h.Address1).HasMaxLength(AddressMaxLength);
        Property(h => h.Address2).HasMaxLength(AddressMaxLength);
        Property(h => h.City).HasMaxLength(CityMaxLength);
        Property(h => h.State).IsAnsi().IsFixedLength().HasMaxLength(2);
        Property(h => h.ZipCode).IsAnsi().HasMaxLength(Domain.ValueObjects.ZipCode.MaxLength);
    }
}
