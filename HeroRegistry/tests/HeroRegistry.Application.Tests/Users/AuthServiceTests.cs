using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Users;
using HeroRegistry.Domain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static HeroRegistry.Application.Tests.Users.UserTestData;

namespace HeroRegistry.Application.Tests.Users;

/// <summary>Registration, login and the refresh-token lifecycle, with mocked ports and a fake clock.</summary>
public class AuthServiceTests
{
    private const string RawRefresh = "raw-refresh-token";
    private static readonly string RefreshHash = new('a', 64);
    private static readonly string NextHash = new('b', 64);
    private static readonly AccessToken Token = new("header.payload.signature", Now.UtcDateTime.AddMinutes(15));

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly FakeLogger<AuthService> _logger = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(StoredHash);
        _tokens.CreateToken(Arg.Any<User>()).Returns(Token);
        _tokens.CreateRefreshToken().Returns(new NewRefreshToken("next-raw-token", NextHash));
        _tokens.HashRefreshToken(RawRefresh).Returns(RefreshHash);
        _service = new AuthService(_users, _hasher, _tokens, _refreshTokens, _currentUser,
            Options.Create(new RefreshTokenOptions()), _clock, _logger);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Registration

    [Fact]
    public async Task Register_stores_a_regular_user_with_a_hashed_password()
    {
        User? saved = null;
        _users.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>()).Returns(5);

        var result = await _service.RegisterAsync(ValidRegistration(), Ct);

        result.Value.Id.ShouldBe(5);
        result.Value.IsAdmin.ShouldBeFalse();
        saved!.Password.ShouldBe(StoredHash);
        saved.Password.ShouldNotBe(StrongPassword);
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
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Register_with_taken_values_returns_Conflict_naming_every_field()
    {
        _users.ExistsByUserNameAsync("peter.parker", null, Arg.Any<CancellationToken>()).Returns(true);
        _users.ExistsByEmailAsync("peter@dailybugle.com", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.RegisterAsync(
            ValidRegistration() with { UserName = "  peter.parker ", Email = " Peter@DailyBugle.com " }, Ct);

        result.Error!.Code.ShouldBe("user.duplicate");
        result.Error.FieldErrors.Keys.ShouldBe(["UserName", "Email"], ignoreOrder: true);
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task Register_turns_a_duplicate_detected_by_the_database_into_Conflict()
    {
        _users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("User", "Email"));

        (await _service.RegisterAsync(ValidRegistration(), Ct)).Error!.FieldErrors.Keys.ShouldBe(["Email"]);
    }

    // Login

    [Fact]
    public async Task Login_issues_an_access_token_and_a_refresh_token_in_a_new_family()
    {
        var user = StoredUser(isAdmin: true);
        _users.GetByUserNameAsync("peter.parker", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(StrongPassword, StoredHash).Returns(true);
        RefreshToken? stored = null;
        _refreshTokens.AddAsync(Arg.Do<RefreshToken>(t => stored = t), Arg.Any<CancellationToken>()).Returns(1);

        var result = await _service.LoginAsync(new LoginRequest { UserName = "peter.parker", Password = StrongPassword }, Ct);

        result.Value.Response.AccessToken.ShouldBe(Token.Token);
        result.Value.Response.TokenType.ShouldBe("Bearer");
        result.Value.Response.User.IsAdmin.ShouldBeTrue();
        result.Value.RefreshToken.ShouldBe("next-raw-token");
        result.Value.RefreshTokenExpiresAt.ShouldBe(Now.UtcDateTime.AddDays(7));
        stored!.UserId.ShouldBe(AgentId);
        stored.TokenHash.ShouldBe(NextHash); // only the hash is stored
        stored.FamilyId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Login_cleans_up_the_users_expired_refresh_tokens()
    {
        _users.GetByUserNameAsync("peter.parker", Arg.Any<CancellationToken>()).Returns(StoredUser());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);

        await _service.LoginAsync(new LoginRequest { UserName = "peter.parker", Password = StrongPassword }, Ct);

        await _refreshTokens.Received(1).DeleteExpiredAsync(AgentId, Now.UtcDateTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_login_starts_its_own_token_family()
    {
        _users.GetByUserNameAsync("peter.parker", Arg.Any<CancellationToken>()).Returns(StoredUser());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
        var families = new List<Guid>();
        _refreshTokens.AddAsync(Arg.Do<RefreshToken>(t => families.Add(t.FamilyId)), Arg.Any<CancellationToken>()).Returns(1);

        await _service.LoginAsync(new LoginRequest { UserName = "peter.parker", Password = StrongPassword }, Ct);
        await _service.LoginAsync(new LoginRequest { UserName = "peter.parker", Password = StrongPassword }, Ct);

        families.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_or_deleted_user_get_the_identical_error_and_no_tokens()
    {
        _users.GetByUserNameAsync("peter.parker", Arg.Any<CancellationToken>()).Returns(StoredUser());

        var wrongPassword = await _service.LoginAsync(new LoginRequest { UserName = "peter.parker", Password = "nope" }, Ct);
        var unknownUser = await _service.LoginAsync(new LoginRequest { UserName = "nobody", Password = "nope" }, Ct);

        wrongPassword.Error.ShouldBe(unknownUser.Error);
        wrongPassword.Error!.Code.ShouldBe("auth.invalid_credentials");
        _tokens.DidNotReceive().CreateToken(Arg.Any<User>());
        _refreshTokens.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unknown_user_still_runs_a_password_verification_to_resist_timing_attacks()
    {
        await _service.LoginAsync(new LoginRequest { UserName = "nobody", Password = "guess" }, Ct);

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
        (await _service.LoginAsync(new LoginRequest { UserName = "Admin" }, Ct)).Error!.Type.ShouldBe(ErrorType.Validation);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void A_session_never_prints_its_refresh_token()
    {
        var session = new AuthSession(new AuthResponse("access", Now.UtcDateTime, new UserResponse(1, "Admin", "a@b.com", true, Now.UtcDateTime)),
            "super-secret-refresh", Now.UtcDateTime);

        session.ToString().ShouldNotContain("super-secret-refresh");
        new NewRefreshToken("super-secret-refresh", NextHash).ToString().ShouldNotContain("super-secret-refresh");
    }

    // Refresh

    [Fact]
    public async Task Refresh_rotates_the_token_within_its_family_and_issues_a_new_access_token()
    {
        var current = StoredRefresh();
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(current);
        _refreshTokens.RevokeAsync(current, Arg.Any<CancellationToken>()).Returns(true);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser());
        RefreshToken? next = null;
        _refreshTokens.AddAsync(Arg.Do<RefreshToken>(t => next = t), Arg.Any<CancellationToken>()).Returns(2);
        _clock.Advance(TimeSpan.FromMinutes(20));

        var result = await _service.RefreshAsync(RawRefresh, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Response.AccessToken.ShouldBe(Token.Token);
        result.Value.RefreshToken.ShouldBe("next-raw-token");
        current.RevokedAt.ShouldBe(Now.UtcDateTime.AddMinutes(20));
        current.ReplacedByHash.ShouldBe(NextHash);
        next!.FamilyId.ShouldBe(current.FamilyId);
        next.TokenHash.ShouldBe(NextHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_without_a_token_is_invalid(string? token)
    {
        (await _service.RefreshAsync(token, Ct)).Error!.Code.ShouldBe("auth.refresh_invalid");
        _refreshTokens.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_is_invalid()
    {
        (await _service.RefreshAsync(RawRefresh, Ct)).Error!.Code.ShouldBe("auth.refresh_invalid");
    }

    [Fact]
    public async Task Refresh_with_an_expired_token_is_invalid_and_revokes_nothing()
    {
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(StoredRefresh());
        _clock.Advance(TimeSpan.FromDays(8));

        (await _service.RefreshAsync(RawRefresh, Ct)).Error!.Code.ShouldBe("auth.refresh_invalid");
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_for_a_deleted_user_is_invalid_and_revokes_the_family()
    {
        var current = StoredRefresh();
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(current);

        (await _service.RefreshAsync(RawRefresh, Ct)).Error!.Code.ShouldBe("auth.refresh_invalid");
        await _refreshTokens.Received(1).RevokeFamilyAsync(current.FamilyId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reuse_inside_the_grace_window_is_a_race_and_revokes_nothing()
    {
        var rotated = StoredRefresh();
        rotated.Revoke(Now.UtcDateTime, NextHash);
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(rotated);
        _clock.Advance(TimeSpan.FromSeconds(30));

        (await _service.RefreshAsync(RawRefresh, Ct)).Error!.Code.ShouldBe("auth.refresh_race");
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reuse_outside_the_grace_window_revokes_the_whole_family_and_logs_a_warning()
    {
        var rotated = StoredRefresh();
        rotated.Revoke(Now.UtcDateTime, NextHash);
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(rotated);
        _clock.Advance(TimeSpan.FromSeconds(31));

        var result = await _service.RefreshAsync(RawRefresh, Ct);

        result.Error!.Code.ShouldBe("auth.refresh_invalid");
        await _refreshTokens.Received(1).RevokeFamilyAsync(rotated.FamilyId, Now.UtcDateTime.AddSeconds(31), Arg.Any<CancellationToken>());
        var warning = _logger.Collector.LatestRecord;
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain($"user {AgentId}");
        warning.Message.ShouldNotContain(RawRefresh);
        warning.Message.ShouldNotContain(RefreshHash);
    }

    [Fact]
    public async Task A_token_revoked_by_logout_is_simply_invalid()
    {
        var loggedOut = StoredRefresh();
        loggedOut.Revoke(Now.UtcDateTime); // no replacement: a plain revocation
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(loggedOut);

        (await _service.RefreshAsync(RawRefresh, Ct)).Error!.Code.ShouldBe("auth.refresh_invalid");
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Losing_the_atomic_rotation_to_a_parallel_request_is_a_race()
    {
        var current = StoredRefresh();
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(current);
        _users.GetByIdAsync(AgentId, Arg.Any<CancellationToken>()).Returns(StoredUser());
        _refreshTokens.RevokeAsync(current, Arg.Any<CancellationToken>()).Returns(false);

        (await _service.RefreshAsync(RawRefresh, Ct)).Error!.Code.ShouldBe("auth.refresh_race");
        await _refreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    // Logout

    [Fact]
    public async Task Logout_revokes_the_family_of_the_presented_token()
    {
        var current = StoredRefresh();
        _refreshTokens.GetByHashAsync(RefreshHash, Arg.Any<CancellationToken>()).Returns(current);

        (await _service.LogoutAsync(RawRefresh, Ct)).IsSuccess.ShouldBeTrue();
        await _refreshTokens.Received(1).RevokeFamilyAsync(current.FamilyId, Now.UtcDateTime, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-token")]
    public async Task Logout_always_succeeds_even_without_a_known_token(string? token)
    {
        (await _service.LogoutAsync(token, Ct)).IsSuccess.ShouldBeTrue();
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    private static RefreshToken StoredRefresh() =>
        RefreshToken.Issue(AgentId, RefreshHash, Guid.NewGuid(), Now.UtcDateTime, TimeSpan.FromDays(7));
}
