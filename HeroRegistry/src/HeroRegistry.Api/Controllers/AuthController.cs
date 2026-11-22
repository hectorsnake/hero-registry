using HeroRegistry.Api.Security;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeroRegistry.Api.Controllers;

/// <summary>Account creation, the session lifecycle (login, refresh, logout) and the current user's profile.</summary>
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IUserService userService, RefreshTokenCookie refreshCookie)
    : ApiControllerBase
{
    /// <summary>Creates a regular user account (AC1). Anonymous.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> Register(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);

        // No Location header: accounts are read through /api/auth/me (own) or /api/users (admins).
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ErrorResult(result.Error!);
    }

    /// <summary>
    /// Exchanges a user name and password for a short-lived bearer token (AC2) and starts a session (AC12).
    /// Anonymous. The refresh token is set as the HttpOnly <c>hr_refresh</c> cookie (Secure, SameSite=Strict,
    /// Path=/api/auth) and is never part of the JSON body.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        SessionResult(await authService.LoginAsync(request, cancellationToken));

    /// <summary>
    /// Silently renews the session (AC12): send the <c>hr_refresh</c> cookie (the browser does it), get a new
    /// access token and a rotated cookie. Anonymous: the expired access token is not needed. A 401 with code
    /// <c>auth.refresh_race</c> means another tab refreshed a moment ago: retry once. Any other 401
    /// (<c>auth.refresh_invalid</c>) means the user must sign in again, and the cookie is removed.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(refreshCookie.Read(Request), cancellationToken);

        // On a race the browser already holds the newer cookie from the winning request: keep it.
        if (!result.IsSuccess && result.Error!.Code != Errors.RefreshRace().Code)
        {
            refreshCookie.Delete(Response);
        }

        return SessionResult(result);
    }

    /// <summary>
    /// Ends the session (AC12): revokes the refresh token (with its whole family) and removes the cookie.
    /// Anonymous and idempotent, so signing out always works, even with an expired access token.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(refreshCookie.Read(Request), cancellationToken);
        refreshCookie.Delete(Response);
        return NoContentOrError(result);
    }

    /// <summary>The signed-in user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken) =>
        OkOrError(await userService.GetCurrentAsync(cancellationToken));

    private ActionResult<AuthResponse> SessionResult(Result<AuthSession> result)
    {
        // Responses carrying tokens must never be stored by a browser or proxy cache.
        Response.Headers.CacheControl = "no-store";

        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!);
        }

        refreshCookie.Write(Response, result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(result.Value.Response);
    }
}
