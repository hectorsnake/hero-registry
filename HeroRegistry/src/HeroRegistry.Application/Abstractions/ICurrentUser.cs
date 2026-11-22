namespace HeroRegistry.Application.Abstractions;

/// <summary>
/// The user making the current request. Implemented by the API from the JWT claims (T09), so the
/// use cases never depend on ASP.NET Core.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The signed-in user's id, or <see langword="null"/> for an anonymous request.</summary>
    int? UserId { get; }

    /// <summary>Whether the signed-in user is an administrator.</summary>
    bool IsAdmin { get; }
}
