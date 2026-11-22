using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Heroes;

namespace HeroRegistry.Application.Heroes;

/// <summary>The hero use cases (list, view, create, update, delete).</summary>
public interface IHeroService
{
    /// <returns>The hero, or <see cref="ErrorType.NotFound"/>.</returns>
    Task<Result<HeroResponse>> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Searches, sorts and pages active heroes. Out-of-range query values are normalized.</summary>
    Task<Result<PagedResult<HeroResponse>>> SearchAsync(HeroQuery? query, CancellationToken cancellationToken = default);

    /// <returns>The new hero, or Unauthorized, Validation or Conflict.</returns>
    Task<Result<HeroResponse>> CreateAsync(CreateHeroRequest request, CancellationToken cancellationToken = default);

    /// <returns>The updated hero, or Unauthorized, Validation, NotFound or Conflict.</returns>
    Task<Result<HeroResponse>> UpdateAsync(int id, UpdateHeroRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a hero. Administrators only.</summary>
    /// <returns>Success, or Unauthorized, Forbidden or NotFound.</returns>
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plain application service (no MediatR): validates input, enforces the rules that need the database
/// (uniqueness), and coordinates the domain entity and the repository. Expected failures are returned
/// as <see cref="Result"/> errors, never thrown.
/// </summary>
public sealed class HeroService(IHeroRepository heroes, ICurrentUser currentUser, TimeProvider clock) : IHeroService
{
    public async Task<Result<HeroResponse>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var hero = await heroes.GetByIdAsync(id, cancellationToken);
        return hero is null ? HeroNotFound(id) : hero.ToResponse();
    }

    public async Task<Result<PagedResult<HeroResponse>>> SearchAsync(HeroQuery? query, CancellationToken cancellationToken = default)
    {
        var page = await heroes.SearchAsync((query ?? new HeroQuery()).Normalize(), cancellationToken);

        return new PagedResult<HeroResponse>(
            page.Items.Select(hero => hero.ToResponse()).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<Result<HeroResponse>> CreateAsync(CreateHeroRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Errors.Unauthorized();
        }

        var now = UtcNow();
        if (Validate(request, now) is { } invalid)
        {
            return invalid;
        }

        SuperHero hero;
        try
        {
            hero = SuperHero.Create(request.ToDetails(), userId, now);
        }
        catch (DomainException ex)
        {
            return ToValidationError(ex);
        }

        // Checked with the normalized values (trimmed name, lower-cased email).
        if (await FindDuplicatesAsync(hero, excludeId: null, cancellationToken) is { } duplicate)
        {
            return duplicate;
        }

        try
        {
            var id = await heroes.AddAsync(hero, cancellationToken);
            return hero.ToResponse() with { Id = id };
        }
        catch (DuplicateEntityException ex)
        {
            // Saved by another request between the check and the insert.
            return DuplicateError(ex.Field);
        }
    }

    public async Task<Result<HeroResponse>> UpdateAsync(int id, UpdateHeroRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Errors.Unauthorized();
        }

        var now = UtcNow();
        if (Validate(request, now) is { } invalid)
        {
            return invalid;
        }

        var hero = await heroes.GetByIdAsync(id, cancellationToken);
        if (hero is null)
        {
            return HeroNotFound(id);
        }

        try
        {
            hero.Update(request.ToDetails(), userId, now);
        }
        catch (DomainException ex)
        {
            return ToValidationError(ex);
        }

        // Excluding the hero itself, so it can keep its own name and email.
        if (await FindDuplicatesAsync(hero, excludeId: id, cancellationToken) is { } duplicate)
        {
            return duplicate;
        }

        try
        {
            return await heroes.UpdateAsync(hero, cancellationToken)
                ? hero.ToResponse()
                : HeroNotFound(id); // deleted by another request meanwhile
        }
        catch (DuplicateEntityException ex)
        {
            return DuplicateError(ex.Field);
        }
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Errors.Unauthorized();
        }

        if (!currentUser.IsAdmin)
        {
            return Errors.Forbidden("Only administrators can delete heroes.");
        }

        var hero = await heroes.GetByIdAsync(id, cancellationToken);
        if (hero is null)
        {
            return HeroNotFound(id);
        }

        hero.SoftDelete(userId, UtcNow());

        return await heroes.SoftDeleteAsync(hero, cancellationToken)
            ? Result.Success()
            : HeroNotFound(id); // deleted by another request meanwhile
    }

    /// <summary>
    /// Safety net: a rule the validator missed but the domain caught becomes a normal validation error
    /// on the same field, rather than a 500.
    /// </summary>
    internal static Error ToValidationError(DomainException exception) =>
        Errors.Validation(new Dictionary<string, string[]> { [exception.Field] = [exception.Message] });

    private DateTime UtcNow() => clock.GetUtcNow().UtcDateTime;

    private static Error? Validate(HeroRequest request, DateTime utcNow)
    {
        var errors = HeroRequestValidator.Validate(request, DateOnly.FromDateTime(utcNow));
        return errors.Count == 0 ? null : Errors.Validation(errors);
    }

    /// <summary>Checks both unique fields, so the client learns about every taken value at once.</summary>
    private async Task<Error?> FindDuplicatesAsync(SuperHero hero, int? excludeId, CancellationToken cancellationToken)
    {
        var taken = new Dictionary<string, string[]>();

        if (await heroes.ExistsByHeroNameAsync(hero.HeroName, excludeId, cancellationToken))
        {
            taken[nameof(SuperHero.HeroName)] = ["Another hero already uses this HeroName."];
        }

        if (await heroes.ExistsByEmailAsync(hero.Email, excludeId, cancellationToken))
        {
            taken[nameof(SuperHero.Email)] = ["Another hero already uses this Email."];
        }

        return taken.Count == 0
            ? null
            : Errors.Conflict("hero.duplicate", "Another hero already uses some of these values.", taken);
    }

    private static Error DuplicateError(string? field) =>
        field is null
            ? Errors.Conflict("hero.duplicate", "Another hero already uses some of these values.")
            : Errors.Conflict(
                "hero.duplicate",
                "Another hero already uses some of these values.",
                new Dictionary<string, string[]> { [field] = [$"Another hero already uses this {field}."] });

    private static Error HeroNotFound(int id) =>
        Errors.NotFound("hero.not_found", $"Hero {id} was not found.");
}
