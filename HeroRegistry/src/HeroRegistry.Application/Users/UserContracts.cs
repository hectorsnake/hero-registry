using System.Text;
using HeroRegistry.Domain.Users;

namespace HeroRegistry.Application.Users;

/// <summary>Body of <c>POST /api/auth/register</c>.</summary>
/// <remarks><see cref="ToString"/> masks the password, so logging the request never leaks it.</remarks>
public sealed record RegisterUserRequest
{
    public string? UserName { get; init; }

    public string? Email { get; init; }

    public string? Password { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("UserName = ").Append(UserName)
            .Append(", Email = ").Append(Email)
            .Append(", Password = ***");
        return true;
    }
}

/// <summary>Body of <c>POST /api/auth/login</c>.</summary>
/// <remarks><see cref="ToString"/> masks the password, so logging the request never leaks it.</remarks>
public sealed record LoginRequest
{
    public string? UserName { get; init; }

    public string? Password { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("UserName = ").Append(UserName).Append(", Password = ***");
        return true;
    }
}

/// <summary>A user account as returned by the API. Never includes the password hash.</summary>
public sealed record UserResponse(int Id, string UserName, string Email, bool IsAdmin, DateTime CreatedAt);

/// <summary>A successful login: the bearer token to send in the Authorization header, and the user.</summary>
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User)
{
    public string TokenType => "Bearer";
}

internal static class UserMapping
{
    public static UserResponse ToResponse(this User user) =>
        new(user.Id, user.UserName, user.Email, user.IsAdmin, user.CreatedAt);
}
