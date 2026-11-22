using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Users;
using HeroRegistry.Domain.Users;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static HeroRegistry.Application.Tests.Users.UserTestData;

namespace HeroRegistry.Application.Tests.Users;

/// <summary>Registration and login, with mocked ports (repository, hasher, token service, current user).</summary>
public class AuthServiceTests
{
    private static readonly AccessToken Token = new("header.payload.signature", Now.UtcDateTime.AddHours(1));

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(StoredHash);
        _tokens.CreateToken(Arg.Any<User>()).Returns(Token);
        _service = new AuthService(_users, _hasher, _tokens, _currentUser, new FakeTimeProvider(Now));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Registration

    [Fact]
    public async Task Register_stores_a_regular_user_with_a_hashed_password()
    {
        User? saved = null;
        _users.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>()).Returns(5);

        var result = await _service.RegisterAsync(ValidRegistration(), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(5);
        result.Value.UserName.ShouldBe("peter.parker");
        result.Value.IsAdmin.ShouldBeFalse();
        saved.ShouldNotBeNull();
        saved.Password.ShouldBe(StoredHash);
        saved.Password.ShouldNotBe(StrongPassword);
        saved.IsAdmin.ShouldBeFalse();
        saved.CreatedAt.ShouldBe(Now.UtcDateTime);
        _hasher.Received(1).Hash(StrongPassword);
    }

    [Fact]
    public async Task Self_registration_has_no_creator_but_an_admin_creating_an_account_is_recorded()
    {
        User? saved = null;
        _users.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>()).Returns(5);

        await _service.RegisterAsync(ValidRegistration(), Ct);
        saved!.CreatedBy.ShouldBeNull();

        _currentUser.UserId.Returns(AdminId);
        await _service.RegisterAsync(ValidRegistration(), Ct);
        saved!.CreatedBy.ShouldBe(AdminId);
    }

    [Fact]
    public async Task Register_with_a_weak_password_is_rejected_without_hashing_or_saving()
    {
        var result = await _service.RegisterAsync(ValidRegistration() with { Password = "password" }, Ct);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(["Password"]);
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Register_with_a_taken_user_name_returns_Conflict_without_hashing()
    {
        _users.ExistsByUserNameAsync("peter.parker", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.RegisterAsync(ValidRegistration(), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("user.duplicate");
        result.Error.FieldErrors.Keys.ShouldBe(["UserName"]);
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_checks_the_email_in_its_normalized_form_and_reports_both_conflicts()
    {
        _users.ExistsByUserNameAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(true);
        _users.ExistsByEmailAsync("peter@dailybugle.com", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.RegisterAsync(
            ValidRegistration() with { UserName = "  peter.parker ", Email = " Peter@DailyBugle.com " }, Ct);

        result.Error!.FieldErrors.Keys.ShouldBe(["UserName", "Email"], ignoreOrder: true);
        await _users.Received(1).ExistsByUserNameAsync("peter.parker", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_turns_a_duplicate_detected_by_the_database_into_Conflict()
    {
        _users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("User", "Email"));

        var result = await _service.RegisterAsync(ValidRegistration(), Ct);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error.FieldErrors.Keys.ShouldBe(["Email"]);
    }

    // Login

    [Fact]
    public async Task Login_with_valid_credentials_returns_a_token_and_the_user()
    {
        var user = StoredUser(isAdmin: true);
        _users.GetByUserNameAsync("peter.parker", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(StrongPassword, StoredHash).Returns(true);

        var result = await _service.LoginAsync(new LoginRequest { UserName = "peter.parker", Password = StrongPassword }, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe(Token.Token);
        result.Value.TokenType.ShouldBe("Bearer");
        result.Value.ExpiresAt.ShouldBe(Token.ExpiresAt);
        result.Value.User.Id.ShouldBe(AgentId);
        result.Value.User.IsAdmin.ShouldBeTrue();
        _tokens.Received(1).CreateToken(user);
    }

    [Fact]
    public async Task Wrong_password_unknown_user_and_deleted_user_all_get_the_identical_error()
    {
        // Wrong password
        _users.GetByUserNameAsync("peter.parker", Arg.Any<CancellationToken>()).Returns(StoredUser());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);
        var wrongPassword = await _service.LoginAsync(new LoginRequest { UserName = "peter.parker", Password = "nope" }, Ct);

        // Unknown or deleted user: the repository returns null for both.
        var unknownUser = await _service.LoginAsync(new LoginRequest { UserName = "nobody", Password = "nope" }, Ct);

        wrongPassword.Error.ShouldBe(unknownUser.Error);
        wrongPassword.Error!.Type.ShouldBe(ErrorType.Unauthorized);
        wrongPassword.Error.Code.ShouldBe("auth.invalid_credentials");
        wrongPassword.Error.Message.ShouldBe("Invalid user name or password.");
        _tokens.DidNotReceive().CreateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task An_unknown_user_still_runs_a_password_verification_to_resist_timing_attacks()
    {
        await _service.LoginAsync(new LoginRequest { UserName = "nobody", Password = "guess" }, Ct);

        // A null hash makes the hasher verify against a dummy hash (same cost as a real check).
        _hasher.Received(1).Verify("guess", null);
    }

    [Fact]
    public async Task The_user_name_is_trimmed_but_the_password_is_used_exactly_as_typed()
    {
        _users.GetByUserNameAsync("peter.parker", Arg.Any<CancellationToken>()).Returns(StoredUser());

        await _service.LoginAsync(new LoginRequest { UserName = " peter.parker ", Password = " spaced " }, Ct);

        _hasher.Received(1).Verify(" spaced ", StoredHash);
    }

    [Fact]
    public async Task Login_with_missing_fields_is_a_validation_error_without_any_lookup()
    {
        var result = await _service.LoginAsync(new LoginRequest { UserName = "Admin" }, Ct);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        _users.ReceivedCalls().ShouldBeEmpty();
        _hasher.ReceivedCalls().ShouldBeEmpty();
    }
}
