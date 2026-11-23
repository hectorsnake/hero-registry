# Generative AI: how this project was generated, validated and corrected

The tool is **Claude Code** (an agentic coding assistant in the terminal).

The exercise asks you to imagine generating a RESTful API with a GenAI tool: CRUD for a resource that belongs to users. This project is the answer to that exercise, built for real rather than imagined. The SuperHero Registry API and its UI were generated with Claude Code, task by task:
- heroes take the place of the exercise's tasks;
- the users and their sign-in are real;
- every hero records who created, updated and deleted it.

This page shows the prompts, a representative sample of the output, how the output was validated, and what was corrected.

## 1. The prompts

The work was split into small prompts, not one large one. Each prompt has three layers.

**1. Shared context, written once and reused by every prompt:**
- **The exercise** (the PDF in `Scrum/`).
- **The user story** [`US-001`](../Scrum/US-001-SuperHero-Registry.md): personas, 12 acceptance criteria in Given/When/Then form, the business rules, and a Definition of Done.
- **Project skills** in `.claude/skills` (`angular-ui`, `angular-unit-test`): the front-end conventions (signals, zoneless, Signal Forms, test ids) and the test-first loop, so they are not repeated in every prompt.
- **Standing constraints:**
  - no Entity Framework, Dapper or MediatR;
  - secrets only in user secrets or `.env`;
  - no personal data, tokens or database messages in logs;
  - don't change the SQL Server instance.

**2. A task file per unit of work** ([`Scrum/Tasks`](../Scrum/Tasks), T01 to T17). Every task file has the same structure. An excerpt from [T07](../Scrum/Tasks/T07-Hero-Use-Cases.md) (the hero use cases):

```markdown
## Goal
Implement the hero business logic (CRUD use cases and validation), independent of the database and the API.

## Steps
- `CreateAsync(request)`:
  1. Requires a signed-in user (`Unauthorized`).
  2. Validates (`Validation`).
  3. `SuperHero.Create` with the current user and the clock.
  4. Checks **both** unique fields with the normalized values (`Conflict` naming every taken field).
  5. Saves. A `DuplicateEntityException` from the database (a race) also becomes `Conflict`.
- `DeleteAsync(id)`: a signed-in user (`Unauthorized`), `IsAdmin` (otherwise `Forbidden`, without
  touching the repository), `NotFound`, then `SoftDelete` with the clock.

## Tests (TDD, NSubstitute mocks for the repository and ICurrentUser, FakeTimeProvider)
- One test per rule, with boundaries: text lengths at min-1, min, max and max+1 ...
- Create: ... duplicate name, duplicate email, both duplicates; uniqueness checked with the normalized
  email; a database duplicate gives Conflict; Unauthorized.

## Done when
- Application coverage is at least 90%.
- The project has no reference to Infrastructure or ASP.NET (the architecture tests pass).
- The Release build has 0 warnings.
```

**3. A short instruction that names the task and the workflow.** For example, the prompt for the Angular core task:

```text
Follow the TDD workflow in T12: write each spec first, run npm test and show it fails, then implement,
and record the red runs in the Implementation notes.
```

**Why it is structured this way:**
- **Context is written once.** The user story, the skills and the constraints are files, so each prompt stays short and no prompt forgets a rule.
- **Explicit steps and edge cases** turn implicit expectations into requirements. These are exactly the places where generated APIs usually go wrong:
  - the order of the checks (authentication, then validation, then the database);
  - races between a uniqueness check and the insert;
  - a delete that must be refused before the repository is touched.
- **Acceptance criteria as tests and "Done when" make the output verifiable.** The tool has to show a failing test first, then a passing one, and then measure coverage and warnings.
- **The output is recorded.** Each task's Implementation notes are written back into the task file: red runs, measured numbers, and what went wrong. That gives a review log, not just code.

## 2. The output (representative sample)

The hero use case generated from T07 ([`HeroService.cs`](../HeroRegistry/src/HeroRegistry.Application/Heroes/HeroService.cs)). The user always comes from the token (`ICurrentUser`), never from the request. Validation runs before any database call. Uniqueness is checked on the normalized values, and the race between that check and the insert is handled:

```csharp
public async Task<Result<HeroResponse>> CreateAsync(CreateHeroRequest request, CancellationToken cancellationToken = default)
{
    if (currentUser.UserId is not { } userId)
        return Errors.Unauthorized();

    var now = UtcNow();
    if (Validate(request, now) is { } invalid)   // every field error at once, no database call
        return invalid;

    var hero = SuperHero.Create(request.ToDetails(), userId, now);

    // Checked with the normalized values (trimmed name, lower-cased email).
    if (await FindDuplicatesAsync(hero, excludeId: null, cancellationToken) is { } duplicate)
        return duplicate;                         // 409 naming every taken field

    try
    {
        var id = await heroes.AddAsync(hero, cancellationToken);
        return hero.ToResponse() with { Id = id };
    }
    catch (DuplicateEntityException ex)
    {
        return DuplicateError(ex.Field);          // saved by another request between the check and the insert
    }
}
```

The authorization rule is in the business layer, so no endpoint can forget it:

```csharp
if (!currentUser.IsAdmin)
    return Errors.Forbidden("Only administrators can delete heroes.");   // before touching the repository
```

The data layer it calls is generated too: ADO.NET with a Reflection mapper, where every value is a SQL parameter (see [architecture](architecture.md#the-reflection-data-layer)).

## 3. How the output was validated

1. **Test-first, with evidence.** Each spec was written and **run red** before the implementation, and the red runs are recorded in each task's Implementation notes (for example, T12: 116 specs).
2. **The tests run against real systems:**
   - **1,116 automated tests:** 763 on the back end, 353 on the front end.
   - **A real SQL Server:** the back-end integration tests use Testcontainers.
   - **The real HTTP pipeline:** the API tests use `WebApplicationFactory` with real signed JWTs.
   - **Architecture tests** fail the build if a layer references the wrong project, or if Entity Framework, Dapper or MediatR is referenced.
3. **The numbers are measured:**
   - builds with **warnings as errors**;
   - coverage per layer from the test reports (Domain 98.3%, Application 98.8%);
   - Angular lint and a clean browser console.
4. **Beyond unit tests:**
   - a **real browser** check of every feature against the running API, including at 360 px;
   - Docker Compose brought up and smoke-tested;
   - a GitHub Actions run on Linux.
5. **A security review against a checklist:**

   | Check | Result |
   | ----- | ------ |
   | The user comes from the token only | `HttpCurrentUser` reads `sub` and the `Admin` role; no request carries a user id |
   | Roles in the business layer | Delete is admin-only in `HeroService`, before the repository is called; the API also returns 403 |
   | Input validation | Every rule in the user story, all field errors at once; out-of-range paging and sort values normalized |
   | Injection | Every value is a SQL parameter; column and table names come from the code-defined maps and are checked against an identifier pattern |
   | Passwords and tokens | BCrypt; a 15-minute JWT kept in memory; a rotating refresh token in an HttpOnly cookie, stored only as a hash, with reuse detection |
   | Secrets | User secrets or `.env` only; the API refuses to start without a signing key |
   | Errors and logs | RFC 9457 problem details, no stack traces or database messages; no personal data or tokens in logs or telemetry |

## 4. What was corrected or improved, and the edge cases

**Edge cases that were added to the prompts (the task files) on purpose:**
- **Duplicates:** name and email are checked after normalization, so `" Black.Widow@HeroRegistry.com "` matches `black.widow@heroregistry.com`. Both fields are reported in one 409. A race with another request becomes a 409, not a 500.
- **Concurrent changes:** a hero deleted by another request while it was being edited or deleted gives 404.
- **Order of checks:** authentication, then validation (before loading anything), then the database.
- **Sessions:**
  - a refresh token used twice revokes its whole family;
  - two tabs refreshing at the same moment get a 30-second grace window instead of being signed out.
- **Time:** `TimeProvider` instead of `DateTime.Now`, so date rules (a birth date not in the future, at most 150 years ago) are deterministic in tests.

**Mistakes in the generated code that verification caught.** Each one was found by a test, the browser, CI or the user, and fixed with a failing test first where it was behavior. Details are in the task notes.

| Where | What was wrong | How it was caught | Fix |
| ----- | -------------- | ----------------- | --- |
| Dev proxy (T13) | It proxied `/api` to the API's **http** endpoint. Under Aspire the API redirects to https, so the browser followed the redirect cross-origin, dropped the token and the cookie, and every login ended at "session expired" | The user, in manual testing | Proxy to the https endpoint. Verified with a second dev server |
| Login and register (T13) | The submit button was disabled while invalid, which **dropped a fast Enter** or an autofill submit | Browser smoke test | Enabled button; an invalid submit shows the errors and focuses the first one |
| Chips input (T14) | After a fast Enter, the text was **not cleared** (a stale `[value]` binding), producing "Weather controlFlight" | Browser smoke test, then a failing spec | The input element is the single source of the text |
| Hero list (T14) | After a delete, **focus was lost**: it moved while the modal dialog still made the page inert | Browser check, then a failing spec | Focus after the render that closes the dialog |
| Hero form (T14) | A 404 in edit mode **crashed**: `httpResource.value()` throws in the error state | The spec | Read the value through `hasValue()` |
| Detail page (T14) | It **overflowed at 360 px** (a grid column could not shrink) | 360 px measurement in the browser | `minmax(0, 1fr)`, stacked on mobile |
| CI (T16) | Code coverage **injected a type** into the Domain assembly on Linux, failing a convention test that passed on Windows | The first CI run | Reproduced locally by forcing the same mode, then the test ignores injected types; the rule is still proven to bite |
| Docs (T15) | A suggested PowerShell key generator used `Get-Random` (**not cryptographic**). The replacement worked only on PowerShell 7 | Self-review, then the user's question | `RandomNumberGenerator`, with a 5.1-compatible version |
| Tests | Several **test** bugs: awaiting stability while a request was deliberately pending (deadlocks), fake timers leaking between tests, a helper reading a two-id `aria-describedby` | Failing or hanging test runs | The specs were fixed, not the code, and recorded as such |

### Suggestions ruled out, and why

| Option | Why it was ruled out |
| ------ | -------------------- |
| Entity Framework, Dapper, MediatR | Forbidden by the exercise. The context states it, and an architecture test fails the build if any of them is referenced |
| Tokens in `localStorage` | Readable by any script (XSS). The access token stays in memory; the refresh token is an HttpOnly cookie |
| `zone.js`, Reactive Forms, `.subscribe()` in components | The project standard is zoneless, signals-first and Signal Forms (T12), enforced by lint rules |
| Adding `curl` to the API image for the healthcheck | A larger attack surface for one probe. bash's `/dev/tcp` does the same |
| Publishing SQL Server on all interfaces in Docker Compose | Bound to `127.0.0.1` only |
| Weakening a failing test to get CI green | The convention test kept its rule and only ignores the tool's injected types, with a check that it still catches a real misplaced type |

## 5. Takeaways

- **Specific prompts with acceptance criteria** produce code that can be checked. Vague prompts produce code that merely looks right.
- **Write the context once:** a user story, task files and skills keep every prompt short and consistent.
- **Green unit tests are necessary, not sufficient.** Most of the mistakes above passed their unit tests and were only visible in a real browser, a real CI runner or a real proxy.
- **Record what the AI got wrong.** The task notes double as a review log.
