using System.Globalization;
using System.Security.Claims;
using HeroRegistry.Application.Abstractions;

namespace HeroRegistry.Api.Security;

/// <summary>Role names carried in the JWT <c>role</c> claim (T08 issues them, T09 authorizes with them).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

/// <summary>
/// The caller of the current HTTP request, read from its authenticated claims: the user id from the
/// <c>sub</c> claim and the administrator flag from the <c>Admin</c> role. Claims of an
/// unauthenticated identity are ignored, because nothing validated them.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int? UserId
    {
        get
        {
            var principal = AuthenticatedUser();
            var subject = principal?.FindFirstValue("sub") ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
                ? id
                : null;
        }
    }

    public bool IsAdmin => UserId is not null && AuthenticatedUser()!.IsInRole(Roles.Admin);

    private ClaimsPrincipal? AuthenticatedUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true ? principal : null;
    }
}
