using HeroRegistry.Api.Security;
using HeroRegistry.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeroRegistry.Api.Controllers;

/// <summary>
/// User administration. Listing and deleting are for administrators. Updating (AC11) is for the account
/// owner or an administrator: that rule depends on the resource (the id), so the use case enforces it,
/// and this action only requires a signed-in user.
/// </summary>
[Route("api/users")]
[Authorize]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    /// <summary>Every active user. Administrators only.</summary>
    [HttpGet]
    [Authorize(Policy = AuthenticationExtensions.AdminOnlyPolicy)]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken) =>
        OkOrError(await userService.ListAsync(cancellationToken));

    /// <summary>
    /// Updates an account's user name and email (AC11). Users can update only their own account
    /// (403 for any other id); administrators can update any account.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        OkOrError(await userService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Soft-deletes a user. Administrators only, never their own account.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthenticationExtensions.AdminOnlyPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken) =>
        NoContentOrError(await userService.DeleteAsync(id, cancellationToken));
}
