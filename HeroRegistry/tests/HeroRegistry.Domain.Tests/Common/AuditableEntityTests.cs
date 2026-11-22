using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Heroes;
using static HeroRegistry.Domain.Tests.TestData;

namespace HeroRegistry.Domain.Tests.Common;

/// <summary>Audit trail and soft delete, exercised through <see cref="Heroes.SuperHero"/>.</summary>
public class AuditableEntityTests
{
    private static readonly DateTime Later = UtcNow.AddHours(2);

    [Fact]
    public void Creation_records_who_and_when_and_leaves_update_fields_empty()
    {
        var hero = CreateHero();

        hero.CreatedBy.ShouldBe(AdminId);
        hero.CreatedAt.ShouldBe(UtcNow);
        hero.UpdatedBy.ShouldBeNull();
        hero.UpdatedAt.ShouldBeNull();
        hero.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void Soft_delete_flags_the_entity_and_records_who_and_when()
    {
        var hero = CreateHero();

        hero.SoftDelete(OtherUserId, Later);

        hero.IsDeleted.ShouldBeTrue();
        hero.UpdatedBy.ShouldBe(OtherUserId);
        hero.UpdatedAt.ShouldBe(Later);
        hero.CreatedBy.ShouldBe(AdminId);
        hero.CreatedAt.ShouldBe(UtcNow);
    }

    [Fact]
    public void Soft_deleting_twice_is_rejected()
    {
        var hero = CreateHero();
        hero.SoftDelete(AdminId, Later);

        Should.Throw<DomainException>(() => hero.SoftDelete(AdminId, Later.AddMinutes(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Audit_requires_a_valid_user_id(int userId)
    {
        Should.Throw<DomainException>(() => CreateHero().SoftDelete(userId, Later));
        Should.Throw<DomainException>(() => SuperHero.Create(Hero(), userId, UtcNow));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Audit_times_must_be_UTC(DateTimeKind kind)
    {
        var notUtc = DateTime.SpecifyKind(Later, kind);

        Should.Throw<DomainException>(() => CreateHero().SoftDelete(AdminId, notUtc));
        Should.Throw<DomainException>(
            () => SuperHero.Create(Hero(), AdminId, DateTime.SpecifyKind(UtcNow, kind)));
    }

    [Fact]
    public void Audit_times_cannot_go_back_before_creation()
    {
        var hero = CreateHero();

        Should.Throw<DomainException>(() => hero.SoftDelete(AdminId, UtcNow.AddSeconds(-1)));
    }
}
