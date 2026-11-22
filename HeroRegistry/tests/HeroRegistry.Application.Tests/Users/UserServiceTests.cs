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
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly UserService _service;

    public UserServiceTests() =>
        _service = new UserService(_users, _currentUser, new FakeTimeProvider(Now));

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

    [Fact]
    public async Task The_password_hash_is_never_part_of_a_user_response()
    {
        SignInAs(AgentId);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser(AgentId));

        var response = (await _service.GetCurrentAsync(Ct)).Value;

        typeof(UserResponse).GetProperties().Select(p => p.Name).ShouldNotContain("Password");
        response.ToString().ShouldNotContain(StoredHash);
    }

    private void SignInAs(int userId, bool isAdmin = false)
    {
        _currentUser.UserId.Returns(userId);
        _currentUser.IsAdmin.Returns(isAdmin);
    }
}
