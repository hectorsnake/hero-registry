using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Heroes;
using HeroRegistry.Domain.Heroes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static HeroRegistry.Application.Tests.Heroes.HeroTestData;
using SortDirection = HeroRegistry.Application.Common.SortDirection;

namespace HeroRegistry.Application.Tests.Heroes;

/// <summary>HeroService with mocked ports (repository, current user) and a fake clock.</summary>
public class HeroServiceTests
{
    private readonly IHeroRepository _repository = Substitute.For<IHeroRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly HeroService _service;

    public HeroServiceTests()
    {
        SignInAs(AgentId, isAdmin: false);
        _service = new HeroService(_repository, _currentUser, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Get

    [Fact]
    public async Task Get_returns_the_hero()
    {
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(ExistingHero(7));

        var result = await _service.GetAsync(7, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.HeroName.ShouldBe("Black Widow");
        result.Value.Superpowers.ShouldBe(["Espionage"]);
        result.Value.CreatedBy.ShouldBe(AdminId);
    }

    [Fact]
    public async Task Get_returns_NotFound_for_a_missing_or_deleted_hero()
    {
        var result = await _service.GetAsync(404, Ct);

        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("hero.not_found");
    }

    // Search

    [Fact]
    public async Task Search_normalizes_the_query_and_maps_the_page()
    {
        HeroQuery? received = null;
        _repository.SearchAsync(Arg.Do<HeroQuery>(q => received = q), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<SuperHero>([ExistingHero(7)], Page: 1, PageSize: 100, TotalCount: 1));

        var result = await _service.SearchAsync(
            new HeroQuery(Search: "  widow ", Page: 0, PageSize: 5000, SortDirection: SortDirection.Descending), Ct);

        received.ShouldBe(new HeroQuery("widow", 1, HeroQuery.MaxPageSize, HeroSortBy.HeroName, SortDirection.Descending));
        result.Value.Items.ShouldHaveSingleItem().HeroName.ShouldBe("Black Widow");
        result.Value.TotalCount.ShouldBe(1);
        result.Value.PageSize.ShouldBe(100);
    }

    [Fact]
    public async Task Search_without_a_query_uses_the_defaults()
    {
        _repository.SearchAsync(Arg.Any<HeroQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<SuperHero>([], 1, 10, 0));

        await _service.SearchAsync(null, Ct);

        await _repository.Received(1).SearchAsync(new HeroQuery(), Arg.Any<CancellationToken>());
    }

    // Create

    [Fact]
    public async Task Create_saves_a_normalized_hero_audited_with_the_current_user_and_clock()
    {
        SuperHero? saved = null;
        _repository.AddAsync(Arg.Do<SuperHero>(h => saved = h), Arg.Any<CancellationToken>()).Returns(42);

        var result = await _service.CreateAsync(ValidCreate() with { State = "ny", HeroName = " Black Widow " }, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(42);
        result.Value.State.ShouldBe("NY");
        saved.ShouldNotBeNull();
        saved.HeroName.ShouldBe("Black Widow");
        saved.CreatedBy.ShouldBe(AgentId);
        saved.CreatedAt.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public async Task Create_with_invalid_data_returns_every_field_error_and_touches_nothing()
    {
        var result = await _service.CreateAsync(ValidCreate() with { PowerLevel = 0, ZipCode = "1" }, Ct);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(["PowerLevel", "ZipCode"], ignoreOrder: true);
        _repository.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_with_a_taken_hero_name_returns_Conflict_on_that_field()
    {
        _repository.ExistsByHeroNameAsync("Black Widow", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.CreateAsync(ValidCreate(), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error.FieldErrors.Keys.ShouldBe(["HeroName"]);
        await _repository.DidNotReceive().AddAsync(Arg.Any<SuperHero>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_reports_both_fields_when_name_and_email_are_taken()
    {
        _repository.ExistsByHeroNameAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(true);
        _repository.ExistsByEmailAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.CreateAsync(ValidCreate(), Ct);

        result.Error!.FieldErrors.Keys.ShouldBe(["HeroName", "Email"], ignoreOrder: true);
    }

    [Fact]
    public async Task Create_checks_uniqueness_with_the_normalized_email()
    {
        await _service.CreateAsync(ValidCreate() with { Email = " Black.Widow@HeroRegistry.com " }, Ct);

        await _repository.Received(1).ExistsByEmailAsync("black.widow@heroregistry.com", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_turns_a_duplicate_detected_by_the_database_into_Conflict()
    {
        // Another request saved the same name between the check and the insert.
        _repository.AddAsync(Arg.Any<SuperHero>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("SuperHero", "HeroName"));

        var result = await _service.CreateAsync(ValidCreate(), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error.FieldErrors.Keys.ShouldBe(["HeroName"]);
    }

    [Fact]
    public async Task Create_requires_a_signed_in_user()
    {
        SignOut();

        var result = await _service.CreateAsync(ValidCreate(), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
        _repository.ReceivedCalls().ShouldBeEmpty();
    }

    // Update

    [Fact]
    public async Task Update_saves_the_changes_with_the_update_audit()
    {
        var hero = ExistingHero(7);
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(hero);
        _repository.UpdateAsync(hero, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.UpdateAsync(7, ValidUpdate(), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PowerLevel.ShouldBe(80);
        result.Value.Address2.ShouldBe("Floor 93");
        result.Value.UpdatedBy.ShouldBe(AgentId);
        result.Value.UpdatedAt.ShouldBe(Now.UtcDateTime);
        result.Value.CreatedBy.ShouldBe(AdminId);
        await _repository.Received(1).UpdateAsync(hero, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_lets_a_hero_keep_its_own_name_and_email()
    {
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(ExistingHero(7));
        _repository.UpdateAsync(Arg.Any<SuperHero>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.UpdateAsync(7, ValidUpdate(), Ct);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).ExistsByHeroNameAsync("Black Widow", 7, Arg.Any<CancellationToken>());
        await _repository.Received(1).ExistsByEmailAsync("black.widow@heroregistry.com", 7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_returns_Conflict_when_another_hero_has_the_email()
    {
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(ExistingHero(7));
        _repository.ExistsByEmailAsync(Arg.Any<string>(), 7, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.UpdateAsync(7, ValidUpdate(), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error.FieldErrors.Keys.ShouldBe(["Email"]);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<SuperHero>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_returns_NotFound_for_a_missing_hero()
    {
        var result = await _service.UpdateAsync(404, ValidUpdate(), Ct);

        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_returns_NotFound_when_the_hero_was_deleted_meanwhile()
    {
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(ExistingHero(7));
        _repository.UpdateAsync(Arg.Any<SuperHero>(), Arg.Any<CancellationToken>()).Returns(false);

        (await _service.UpdateAsync(7, ValidUpdate(), Ct)).Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_with_invalid_data_is_rejected_before_loading_the_hero()
    {
        var result = await _service.UpdateAsync(7, ValidUpdate() with { Superpowers = [] }, Ct);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(["Superpowers"]);
        _repository.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_turns_a_duplicate_detected_by_the_database_into_Conflict()
    {
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(ExistingHero(7));
        _repository.UpdateAsync(Arg.Any<SuperHero>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("SuperHero", "Email"));

        (await _service.UpdateAsync(7, ValidUpdate(), Ct)).Error!.FieldErrors.Keys.ShouldBe(["Email"]);
    }

    [Fact]
    public async Task Update_requires_a_signed_in_user()
    {
        SignOut();

        (await _service.UpdateAsync(7, ValidUpdate(), Ct)).Error!.Type.ShouldBe(ErrorType.Unauthorized);
        _repository.ReceivedCalls().ShouldBeEmpty();
    }

    // Delete

    [Fact]
    public async Task Delete_by_an_admin_soft_deletes_the_hero()
    {
        SignInAs(AdminId, isAdmin: true);
        var hero = ExistingHero(7);
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(hero);
        _repository.SoftDeleteAsync(hero, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.DeleteAsync(7, Ct);

        result.IsSuccess.ShouldBeTrue();
        hero.IsDeleted.ShouldBeTrue();
        hero.UpdatedBy.ShouldBe(AdminId);
        hero.UpdatedAt.ShouldBe(Now.UtcDateTime);
        await _repository.Received(1).SoftDeleteAsync(hero, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_by_a_regular_user_is_Forbidden_and_touches_nothing()
    {
        var result = await _service.DeleteAsync(7, Ct);

        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
        _repository.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_of_a_missing_hero_returns_NotFound()
    {
        SignInAs(AdminId, isAdmin: true);

        (await _service.DeleteAsync(404, Ct)).Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Delete_returns_NotFound_when_the_hero_was_deleted_meanwhile()
    {
        SignInAs(AdminId, isAdmin: true);
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(ExistingHero(7));
        _repository.SoftDeleteAsync(Arg.Any<SuperHero>(), Arg.Any<CancellationToken>()).Returns(false);

        (await _service.DeleteAsync(7, Ct)).Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Delete_requires_a_signed_in_user()
    {
        SignOut();

        (await _service.DeleteAsync(7, Ct)).Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    // Safety net

    [Fact]
    public void A_DomainException_becomes_a_validation_error_on_its_field()
    {
        var error = HeroService.ToValidationError(new Domain.Common.DomainException("BirthDate", "BirthDate must be in the past."));

        error.Type.ShouldBe(ErrorType.Validation);
        error.FieldErrors["BirthDate"].ShouldBe(["BirthDate must be in the past."]);
    }

    private void SignInAs(int userId, bool isAdmin)
    {
        _currentUser.UserId.Returns(userId);
        _currentUser.IsAdmin.Returns(isAdmin);
    }

    private void SignOut()
    {
        _currentUser.UserId.Returns((int?)null);
        _currentUser.IsAdmin.Returns(false);
    }
}
