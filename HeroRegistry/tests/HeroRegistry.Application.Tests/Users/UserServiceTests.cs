using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Users;
using HeroRegistry.Domain.Users;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using static HeroRegistry.Application.Tests.Users.UserTestData;

namespace HeroRegistry.Application.Tests.Users;

/// <summary>Account management: the current user's profile (any user) and user administration (admins only).</summary>
public class UserServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly UserService _service;

    public UserServiceTests() =>
        _service = new UserService(_users, _refreshTokens, _currentUser, new FakeTimeProvider(Now));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Current user (GET /api/auth/me)

    [Fact]
    public async Task GetCurrent_returns_the_signed_in_users_profile()
    {
        SignInAs(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));

        var result = await _service.GetCurrentAsync(Ct);

        result.Value.Id.ShouldBe(AgentId);
        result.Value.UserName.ShouldBe("peter.parker");
    }

    [Fact]
    public async Task GetCurrent_requires_a_signed_in_user()
    {
        (await _service.GetCurrentAsync(Ct)).Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task A_valid_token_of_a_deleted_account_is_Unauthorized()
    {
        SignInAs(AgentId);

        (await _service.GetCurrentAsync(Ct)).Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    // List (admins only)

    [Fact]
    public async Task An_admin_can_list_users()
    {
        SignInAs(AdminId, isAdmin: true);
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([StoredUser(AdminId, "Admin", isAdmin: true), StoredUser()]);

        var result = await _service.ListAsync(Ct);

        result.Value.Select(u => u.UserName).ShouldBe(["Admin", "peter.parker"]);
    }

    [Fact]
    public async Task A_regular_user_cannot_list_users()
    {
        SignInAs(AgentId);

        (await _service.ListAsync(Ct)).Error!.Type.ShouldBe(ErrorType.Forbidden);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Listing_requires_a_signed_in_user()
    {
        (await _service.ListAsync(Ct)).Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    // Delete (admins only, never themselves)

    [Fact]
    public async Task An_admin_can_soft_delete_another_user()
    {
        SignInAs(AdminId, isAdmin: true);
        var user = StoredUser(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(user);
        _users.SoftDeleteAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.DeleteAsync(AgentId, Ct);

        result.IsSuccess.ShouldBeTrue();
        user.IsDeleted.ShouldBeTrue();
        user.UpdatedBy.ShouldBe(AdminId);
        user.UpdatedAt.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public async Task Deleting_a_user_revokes_all_of_their_refresh_tokens()
    {
        SignInAs(AdminId, isAdmin: true);
        var user = StoredUser(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(user);
        _users.SoftDeleteAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        await _service.DeleteAsync(AgentId, Ct);

        await _refreshTokens.Received(1).RevokeAllForUserAsync(AgentId, Now.UtcDateTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_delete_does_not_touch_refresh_tokens()
    {
        SignInAs(AdminId, isAdmin: true);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));
        _users.SoftDeleteAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(false);

        await _service.DeleteAsync(AgentId, Ct);

        _refreshTokens.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_admin_cannot_delete_their_own_account()
    {
        SignInAs(AdminId, isAdmin: true);

        var result = await _service.DeleteAsync(AdminId, Ct);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(["Id"]);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_regular_user_cannot_delete_users()
    {
        SignInAs(AgentId);

        (await _service.DeleteAsync(3, Ct)).Error!.Type.ShouldBe(ErrorType.Forbidden);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_a_missing_user_returns_NotFound()
    {
        SignInAs(AdminId, isAdmin: true);

        var result = await _service.DeleteAsync(404, Ct);

        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("user.not_found");
    }

    [Fact]
    public async Task Deleting_a_user_deleted_meanwhile_returns_NotFound()
    {
        SignInAs(AdminId, isAdmin: true);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));
        _users.SoftDeleteAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(false);

        (await _service.DeleteAsync(AgentId, Ct)).Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Deleting_requires_a_signed_in_user()
    {
        (await _service.DeleteAsync(AgentId, Ct)).Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    // Update (AC11): owners update themselves, admins update anyone

    [Fact]
    public async Task A_user_can_update_their_own_account()
    {
        SignInAs(AgentId);
        var user = StoredUser(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(user);
        _users.UpdateAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.UpdateAsync(AgentId, Update("spider.man", "Spidey@Avengers.org"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.UserName.ShouldBe("spider.man");
        result.Value.Email.ShouldBe("spidey@avengers.org");
        user.UpdatedBy.ShouldBe(AgentId);
        user.UpdatedAt.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public async Task An_admin_can_update_any_account()
    {
        SignInAs(AdminId, isAdmin: true);
        var user = StoredUser(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(user);
        _users.UpdateAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.UpdateAsync(AgentId, Update("spider.man", "spidey@avengers.org"), Ct);

        result.IsSuccess.ShouldBeTrue();
        user.UpdatedBy.ShouldBe(AdminId);
    }

    [Theory]
    [InlineData(3)]       // another existing account
    [InlineData(99_999)]  // an id that does not exist: still Forbidden, so existence is not revealed
    public async Task A_regular_user_cannot_update_another_account_and_nothing_is_looked_up(int otherId)
    {
        SignInAs(AgentId);

        var result = await _service.UpdateAsync(otherId, Update("hacker", "hacker@evil.com"), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Updating_requires_a_signed_in_user()
    {
        (await _service.UpdateAsync(AgentId, Update("spider.man", "spidey@avengers.org"), Ct))
            .Error!.Type.ShouldBe(ErrorType.Unauthorized);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_data_is_rejected_with_every_field_error_before_any_lookup()
    {
        SignInAs(AgentId);

        var result = await _service.UpdateAsync(AgentId, Update("x", "not-an-email"), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(["UserName", "Email"], ignoreOrder: true);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Updating_a_missing_account_returns_NotFound()
    {
        SignInAs(AdminId, isAdmin: true);

        (await _service.UpdateAsync(404, Update("spider.man", "spidey@avengers.org"), Ct))
            .Error!.Code.ShouldBe("user.not_found");
    }

    [Fact]
    public async Task An_account_deleted_meanwhile_returns_NotFound()
    {
        SignInAs(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));
        _users.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(false);

        (await _service.UpdateAsync(AgentId, Update("spider.man", "spidey@avengers.org"), Ct))
            .Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task A_user_name_or_email_of_another_account_is_a_Conflict_naming_both_fields()
    {
        SignInAs(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));
        _users.ExistsByUserNameAsync("Admin", AgentId, Arg.Any<CancellationToken>()).Returns(true);
        _users.ExistsByEmailAsync("admin@heroregistry.com", AgentId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.UpdateAsync(AgentId, Update("Admin", "Admin@HeroRegistry.com"), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("user.duplicate");
        result.Error.FieldErrors.Keys.ShouldBe(["UserName", "Email"], ignoreOrder: true);
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Keeping_your_own_values_is_allowed_because_uniqueness_excludes_your_account()
    {
        SignInAs(AgentId);
        var user = StoredUser(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(user);
        _users.UpdateAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.UpdateAsync(AgentId, Update(user.UserName, user.Email), Ct);

        result.IsSuccess.ShouldBeTrue();
        await _users.Received(1).ExistsByUserNameAsync(user.UserName, AgentId, Arg.Any<CancellationToken>());
        await _users.Received(1).ExistsByEmailAsync(user.Email, AgentId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_duplicate_detected_by_the_database_is_a_Conflict()
    {
        SignInAs(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));
        _users.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new DuplicateEntityException("User", "UserName"));

        (await _service.UpdateAsync(AgentId, Update("spider.man", "spidey@avengers.org"), Ct))
            .Error!.FieldErrors.Keys.ShouldBe(["UserName"]);
    }

    [Fact]
    public async Task The_password_hash_is_never_part_of_a_user_response()
    {
        SignInAs(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));

        var response = (await _service.GetCurrentAsync(Ct)).Value;

        typeof(UserResponse).GetProperties().Select(p => p.Name).ShouldNotContain("Password");
        response.ToString().ShouldNotContain(StoredHash);
    }

    private static UpdateUserRequest Update(string userName, string email) => new() { UserName = userName, Email = email };

    private void SignInAs(int userId, bool isAdmin = false)
    {
        _currentUser.UserId.Returns(userId);
        _currentUser.IsAdmin.Returns(isAdmin);
    }
}
