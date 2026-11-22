using HeroRegistry.Api.Security;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Heroes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeroRegistry.Api.Controllers;

/// <summary>
/// The hero registry (AC4 to AC8). Thin by design: each action delegates to <see cref="IHeroService"/>,
/// which validates, enforces the business rules and the admin-only delete, and the base class maps the
/// result to HTTP. The authorization attributes are defense in depth.
/// </summary>
[Route("api/heroes")]
[Authorize]
public sealed class HeroesController(IHeroService heroes) : ApiControllerBase
{
    /// <summary>
    /// Active heroes, searched, sorted and paged (AC5). <c>search</c> matches hero name, first name,
    /// last name, city or a superpower; <c>sortBy</c> is <c>HeroName</c>, <c>PowerLevel</c> or
    /// <c>CreatedAt</c>; <c>sortDirection</c> is <c>Ascending</c> or <c>Descending</c>. Out-of-range
    /// paging values are normalized (page at least 1, page size 1 to 100).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<HeroResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<HeroResponse>>> Search([FromQuery] HeroQuery query, CancellationToken cancellationToken) =>
        OkOrError(await heroes.SearchAsync(query, cancellationToken));

    /// <summary>One hero, with its audit trail (AC6).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<HeroResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<HeroResponse>> Get(int id, CancellationToken cancellationToken) =>
        OkOrError(await heroes.GetAsync(id, cancellationToken));

    /// <summary>Registers a hero, audited with the signed-in user (AC4). The Location header points to it.</summary>
    [HttpPost]
    [ProducesResponseType<HeroResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<HeroResponse>> Create(CreateHeroRequest request, CancellationToken cancellationToken)
    {
        var result = await heroes.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : ErrorResult(result.Error!);
    }

    /// <summary>Replaces a hero's details, audited with the signed-in user (AC7).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<HeroResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<HeroResponse>> Update(int id, UpdateHeroRequest request, CancellationToken cancellationToken) =>
        OkOrError(await heroes.UpdateAsync(id, request, cancellationToken));

    /// <summary>Soft-deletes a hero (AC8). Administrators only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthenticationExtensions.AdminOnlyPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken) =>
        NoContentOrError(await heroes.DeleteAsync(id, cancellationToken));
}
