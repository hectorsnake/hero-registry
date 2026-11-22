using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Common;

namespace HeroRegistry.Application.Users;

/// <summary>The current user's profile and user administration.</summary>
public interface IUserService
{
    /// <summary>The signed-in user's own profile (<c>GET /api/auth/me</c>).</summary>
    /// <returns>The user, or Unauthorized (not signed in, or the account was deleted).</returns>
    Task<Result<UserResponse>> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>Every active user. Administrators only.</summary>
    Task<Result<IReadOnlyList<UserResponse>>> ListAsync(CancellationToken cancellationToken = default);

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

    private static Error UserNotFound(int id) => Errors.NotFound("user.not_found", $"User {id} was not found.");
}
