using System.Data.Common;
using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeroRegistry.Api.Tests.CrossCutting;

/// <summary>
/// Test-only endpoints (added to the API under test as an application part) that throw the kinds of
/// exceptions the global exception handler must deal with.
/// </summary>
[ApiController]
[Route("test-only/throw")]
[AllowAnonymous]
public sealed class ThrowingController : ControllerBase
{
    public const string PersonalData = "someone@example.com";

    [HttpGet("bug")]
    public IActionResult Bug() =>
        throw new InvalidOperationException($"Unexpected state for {PersonalData}");

    [HttpGet("database")]
    public IActionResult Database() =>
        throw new FakeDbException($"The duplicate key value is ({PersonalData}).");

    [HttpGet("wrapped-database")]
    public IActionResult WrappedDatabase() =>
        throw new InvalidOperationException("Save failed", new FakeDbException($"Violation for ({PersonalData})."));

    [HttpGet("duplicate")]
    public IActionResult Duplicate() =>
        throw new DuplicateEntityException("SuperHero", "HeroName");

    [HttpGet("domain")]
    public IActionResult Domain() =>
        throw new DomainException("BirthDate", "BirthDate must be in the past.");

    public sealed class FakeDbException(string message) : DbException(message);
}
