using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Users;
using HeroRegistry.Domain.ValueObjects;

namespace HeroRegistry.Application.Users;

/// <summary>The current user's profile and user administration.</summary>
public interface IUserService
{
    /// <summary>The signed-in user's own profile (<c>GET /api/auth/me</c>).</summary>
    /// <returns>The user, or Unauthorized (not signed in, or the account was deleted).</returns>
    Task<Result<UserResponse>> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>Every active user. Administrators only.</summary>
    Task<Result<IReadOnlyList<UserResponse>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an account's user name and email (AC11): users can update only their own account,
    /// administrators can update any account.
    /// </summary>
    /// <returns>The updated user, or Unauthorized, Forbidden, Validation, NotFound or Conflict.</returns>
    Task<Result<UserResponse>> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a user. Administrators only, and never their own account.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class UserService(IUserRepository users, ICurrentUser currentUser, TimeProvider clock) : IUserService
{
    public async Task<Result<UserResponse>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Errors.Unauthorized();
        }

        // A token stays valid until it expires; an account deleted meanwhile must not keep working.
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null ? Errors.Unauthorized("This account no longer exists.") : user.ToResponse();
    }

    public async Task<Result<IReadOnlyList<UserResponse>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        var list = await users.ListAsync(cancellationToken);
        return list.Select(user => user.ToResponse()).ToList();
    }

    public async Task<Result<UserResponse>> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Errors.Unauthorized();
        }

        // Owner or administrator. Checked before any lookup, so a regular user cannot learn
        // whether another user id exists (403 for every id that is not their own).
        if (id != userId && !currentUser.IsAdmin)
        {
            return Errors.Forbidden("You can only update your own account.");
        }

        var errors = UserRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Errors.Validation(errors);
        }

        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return UserNotFound(id);
        }

        var userName = request.UserName!.Trim();
        var email = EmailAddress.Create(request.Email).Value;

        // Excluding the account itself, so it can keep its own values.
        if (await FindDuplicatesAsync(userName, email, id, cancellationToken) is { } duplicate)
        {
            return duplicate;
        }

        try
        {
            user.UpdateAccount(userName, email, userId, clock.GetUtcNow().UtcDateTime);
        }
        catch (DomainException ex)
        {
            return Errors.Validation(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }

        try
        {
            return await users.UpdateAsync(user, cancellationToken)
                ? user.ToResponse()
                : UserNotFound(id); // deleted by another request meanwhile
        }
        catch (DuplicateEntityException ex)
        {
            return ex.Field is null
                ? Errors.Conflict("user.duplicate", "An account already uses some of these values.")
                : Errors.Conflict(
                    "user.duplicate",
                    "An account already uses some of these values.",
                    new Dictionary<string, string[]> { [ex.Field] = [$"This {ex.Field} is already in use."] });
        }
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        var adminId = currentUser.UserId!.Value;
        if (id == adminId)
        {
            // The registry must always keep an administrator who can sign in.
            return Errors.Validation(new Dictionary<string, string[]> { ["Id"] = ["You cannot delete your own account."] });
        }

        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return UserNotFound(id);
        }

        user.SoftDelete(adminId, clock.GetUtcNow().UtcDateTime);

        return await users.SoftDeleteAsync(user, cancellationToken)
            ? Result.Success()
            : UserNotFound(id); // deleted by another request meanwhile
    }

    private Error? RequireAdmin()
    {
        if (currentUser.UserId is null)
        {
            return Errors.Unauthorized();
        }

        return currentUser.IsAdmin ? null : Errors.Forbidden("Only administrators can manage users.");
    }

    private async Task<Error?> FindDuplicatesAsync(string userName, string email, int excludeId, CancellationToken cancellationToken)
    {
        var taken = new Dictionary<string, string[]>();

        if (await users.ExistsByUserNameAsync(userName, excludeId, cancellationToken))
        {
            taken[nameof(User.UserName)] = ["This UserName is already taken."];
        }

        if (await users.ExistsByEmailAsync(email, excludeId, cancellationToken))
        {
            taken[nameof(User.Email)] = ["This Email is already registered."];
        }

        return taken.Count == 0
            ? null
            : Errors.Conflict("user.duplicate", "An account already uses some of these values.", taken);
    }

    private static Error UserNotFound(int id) => Errors.NotFound("user.not_found", $"User {id} was not found.");
}
