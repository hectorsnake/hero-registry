using System.Reflection;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Heroes;
using HeroRegistry.Domain.Users;

namespace HeroRegistry.Domain.Tests;

/// <summary>
/// Entities can only change through their methods (Create, Update, SoftDelete...), so they are always
/// valid. The Reflection data mapper (T05) can still load them from the database through the private
/// parameterless constructor and private setters.
/// </summary>
public class EncapsulationTests
{
    public static TheoryData<Type> EntityTypes() =>
        new(AssemblyReference.Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(AuditableEntity))));

    [Fact]
    public void The_domain_has_the_SuperHero_and_User_entities()
    {
        EntityTypes().Select(row => row.Data).ShouldBe([typeof(SuperHero), typeof(User)], ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(EntityTypes))]
    public void Entities_have_no_public_setters(Type entityType)
    {
        var publicSetters = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name);

        publicSetters.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(EntityTypes))]
    public void Entities_have_no_public_constructors(Type entityType)
    {
        entityType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(EntityTypes))]
    public void Entities_can_be_hydrated_by_the_Reflection_mapper(Type entityType)
    {
        // What the T05 mapper does: create through the private parameterless constructor,
        // then set every property (private setters included) from the row's columns.
        var entity = Activator.CreateInstance(entityType, nonPublic: true).ShouldNotBeNull();

        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        properties.ShouldAllBe(p => p.SetMethod != null, "every mapped property needs a (private) setter");

        entityType.GetProperty(nameof(AuditableEntity.Id))!.SetValue(entity, 42);
        ((AuditableEntity)entity).Id.ShouldBe(42);
    }

    [Fact]
    public void A_hydrated_hero_exposes_the_loaded_values()
    {
        var hero = (SuperHero)Activator.CreateInstance(typeof(SuperHero), nonPublic: true)!;

        typeof(SuperHero).GetProperty(nameof(SuperHero.HeroName))!.SetValue(hero, "Superman");
        typeof(SuperHero).GetProperty(nameof(SuperHero.Superpowers))!.SetValue(hero, new[] { "Flight" });

        hero.HeroName.ShouldBe("Superman");
        hero.Superpowers.ShouldBe(["Flight"]);
    }

    [Fact]
    public void A_hydrated_entity_starts_with_safe_defaults()
    {
        var hero = (SuperHero)Activator.CreateInstance(typeof(SuperHero), nonPublic: true)!;

        hero.HeroName.ShouldBe(string.Empty);
        hero.Superpowers.ShouldBeEmpty();
    }
}
