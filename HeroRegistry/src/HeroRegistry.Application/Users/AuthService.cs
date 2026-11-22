using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Users;
using HeroRegistry.Domain.ValueObjects;

namespace HeroRegistry.Application.Users;

/// <summary>Account sign-up and login.</summary>
public interface IAuthService
{
    /// <summary>Creates a regular (never admin) account.</summary>
    /// <returns>The new user, or Validation or Conflict.</returns>
    Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Checks the credentials and issues an access token.</summary>
    /// <returns>The token and user, Validation (missing fields), or the single <c>auth.invalid_credentials</c> error.</returns>
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registration and login. Passwords are only ever handled in memory: they are hashed before being
/// stored, never logged (the request types mask them) and never returned.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    ICurrentUser currentUser,
    TimeProvider clock) : IAuthService
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
            user = User.Register(userName, email, passwordHash, clock.GetUtcNow().UtcDateTime, currentUser.UserId);
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

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
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

        var token = tokens.CreateToken(user);
        return new AuthResponse(token.Token, token.ExpiresAt, user.ToResponse());
    }

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
}
