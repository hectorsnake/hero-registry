using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Users;
using HeroRegistry.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Application.Users;

/// <summary>Account sign-up, login and the session lifecycle (refresh, logout).</summary>
public interface IAuthService
{
    /// <summary>Creates a regular (never admin) account.</summary>
    /// <returns>The new user, or Validation or Conflict.</returns>
    Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Checks the credentials and starts a session: an access token and a new refresh token family.</summary>
    /// <returns>The session, Validation (missing fields), or the single <c>auth.invalid_credentials</c> error.</returns>
    Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Rotates the refresh token and issues a new access token (AC12).</summary>
    /// <returns>The new session, <c>auth.refresh_invalid</c>, or <c>auth.refresh_race</c> (retry once).</returns>
    Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Ends the session: revokes the refresh token's family. Idempotent; always succeeds.</summary>
    Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registration, login and the refresh-token lifecycle. Passwords and refresh tokens are only ever handled
/// in memory: passwords are hashed with BCrypt and refresh tokens with SHA-256 before being stored, and
/// neither is ever logged or returned in a response body.
/// </summary>
public sealed partial class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    ICurrentUser currentUser,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = UserRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Errors.Validation(errors);
        }

        // Uniqueness is checked with the values as they will be stored.
        var userName = request.UserName!.Trim();
        var email = EmailAddress.Create(request.Email).Value;

        if (await FindDuplicatesAsync(userName, email, cancellationToken) is { } duplicate)
        {
            return duplicate;
        }

        // Hashing is deliberately slow (BCrypt), so it only runs once the request is known to be valid.
        var passwordHash = passwordHasher.Hash(request.Password!);

        User user;
        try
        {
            // Self-registration has no creator; an administrator creating an account is recorded.
            user = User.Register(userName, email, passwordHash, UtcNow(), currentUser.UserId);
        }
        catch (DomainException ex)
        {
            return Errors.Validation(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }

        try
        {
            var id = await users.AddAsync(user, cancellationToken);
            return user.ToResponse() with { Id = id };
        }
        catch (DuplicateEntityException ex)
        {
            // Registered by another request between the check and the insert.
            return DuplicateError(ex.Field);
        }
    }

    public async Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = UserRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Errors.Validation(errors);
        }

        // Deleted users are not returned, so they fail exactly like unknown ones.
        var user = await users.GetByUserNameAsync(request.UserName!.Trim(), cancellationToken);

        // Always verify, even without a user (null hash = dummy verification), so the response time
        // does not reveal whether the user name exists. The password is used exactly as typed.
        var passwordMatches = passwordHasher.Verify(request.Password!, user?.Password);

        if (user is null || !passwordMatches)
        {
            return Errors.InvalidCredentials();
        }

        var now = UtcNow();

        // Housekeeping: expired tokens of this user are no longer needed, even for reuse detection.
        await refreshTokens.DeleteExpiredAsync(user.Id, now, cancellationToken);

        // Each login starts a new token family.
        return await StartSessionAsync(user, Guid.NewGuid(), now, cancellationToken);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Errors.RefreshInvalid();
        }

        var stored = await refreshTokens.GetByHashAsync(tokens.HashRefreshToken(refreshToken), cancellationToken);
        if (stored is null)
        {
            return Errors.RefreshInvalid();
        }

        var now = UtcNow();
        var options = refreshTokenOptions.Value;

        if (!stored.IsActive(now))
        {
            if (stored.WasRotatedWithin(now, options.ReuseGrace))
            {
                // Two tabs refreshing at once: the other one won. Not a theft; nothing is revoked.
                return Errors.RefreshRace();
            }

            if (stored.ReplacedByHash is not null && stored.ExpiresAt > now)
            {
                // An already-rotated token presented again, well after its rotation: probably stolen.
                // Revoke the whole family so neither the thief nor the victim can keep using it.
                await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
                LogReuseDetected(logger, stored.UserId);
            }

            return Errors.RefreshInvalid();
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            // The account was deleted (its tokens are revoked then; this covers any leftover).
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            return Errors.RefreshInvalid();
        }

        // Rotate: revoke the presented token, pointing to its replacement. Atomic: if another request
        // rotated it in the meantime, that request won the race.
        var next = tokens.CreateRefreshToken();
        stored.Revoke(now, next.Hash);
        if (!await refreshTokens.RevokeAsync(stored, cancellationToken))
        {
            return Errors.RefreshRace();
        }

        return await StartSessionAsync(user, stored.FamilyId, now, cancellationToken, next);
    }

    public async Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var stored = await refreshTokens.GetByHashAsync(tokens.HashRefreshToken(refreshToken), cancellationToken);
            if (stored is not null)
            {
                await refreshTokens.RevokeFamilyAsync(stored.FamilyId, UtcNow(), cancellationToken);
            }
        }

        // Signing out an unknown or already ended session is not an error.
        return Result.Success();
    }

    private async Task<Result<AuthSession>> StartSessionAsync(
        User user,
        Guid familyId,
        DateTime now,
        CancellationToken cancellationToken,
        NewRefreshToken? refresh = null)
    {
        refresh ??= tokens.CreateRefreshToken();
        var stored = RefreshToken.Issue(user.Id, refresh.Hash, familyId, now, refreshTokenOptions.Value.Lifetime);
        await refreshTokens.AddAsync(stored, cancellationToken);

        var access = tokens.CreateToken(user);
        return new AuthSession(
            new AuthResponse(access.Token, access.ExpiresAt, user.ToResponse()),
            refresh.Token,
            stored.ExpiresAt);
    }

    private DateTime UtcNow() => clock.GetUtcNow().UtcDateTime;

    private async Task<Error?> FindDuplicatesAsync(string userName, string email, CancellationToken cancellationToken)
    {
        var taken = new Dictionary<string, string[]>();

        if (await users.ExistsByUserNameAsync(userName, excludeId: null, cancellationToken))
        {
            taken[nameof(User.UserName)] = ["This UserName is already taken."];
        }

        if (await users.ExistsByEmailAsync(email, excludeId: null, cancellationToken))
        {
            taken[nameof(User.Email)] = ["This Email is already registered."];
        }

        return taken.Count == 0
            ? null
            : Errors.Conflict("user.duplicate", "An account already uses some of these values.", taken);
    }

    private static Error DuplicateError(string? field) =>
        field is null
            ? Errors.Conflict("user.duplicate", "An account already uses some of these values.")
            : Errors.Conflict(
                "user.duplicate",
                "An account already uses some of these values.",
                new Dictionary<string, string[]> { [field] = [$"This {field} is already in use."] });

    // The user id only: never the token or its hash.
    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserId}; the whole token family was revoked")]
    private static partial void LogReuseDetected(ILogger logger, int userId);
}
