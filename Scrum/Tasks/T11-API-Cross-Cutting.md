# T11: API cross-cutting concerns

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** API · **Estimate:** 3 h · **Depends on:** T01, T09, T10 · **Status:** Done

## Goal
Add consistent error handling, documentation, CORS, health checks and configuration to the API.

## Steps
- [x] Global exception handling (`Api/ErrorHandling/GlobalExceptionHandler.cs`, an `IExceptionHandler`) with `AddProblemDetails()`:
  - Unhandled exceptions return a **500 problem** ("An unexpected error occurred.", code `server.error`) with **no message, type or stack trace, in any environment**.
  - Safety nets: a `DuplicateEntityException` that escapes the use cases returns 409 (code `duplicate`, `errors.<field>`), and a `DomainException` returns 400 on its camelCase field.
  - A client disconnect returns 499, with no body and no error log.
  - `traceId` is in every problem. It contains the W3C trace id, which is Application Insights' `operation_Id`.
  - **Privacy (T02.2):**
    - **Database exceptions**, the exception itself or any inner one being a `DbException`, are logged and traced with their **type and SQL error number only**. Their message can echo data.
    - Code bugs are logged with the full exception, for diagnosis.
    - .NET 10's exception-handler middleware suppresses its own logging when an `IExceptionHandler` handles the exception, so this handler is the only place that logs it.
    - ServiceDefaults' ASP.NET Core instrumentation now has `RecordException = false`; the handler records exceptions on the request span instead (full for bugs, type-only for database errors).
- [x] `InvalidModelStateResponseFactory` (`ProblemDetailsSetup.InvalidModelStateResponse`): model binding and JSON errors use **the same shape as the use-case validation errors**.
  - Code `validation.failed`, with camelCase keys: `$.birthDate` becomes `birthDate`, `SortBy` becomes `sortBy`, and the body itself becomes `body`.
  - Readable messages with no .NET type names or line numbers: "birthDate has an invalid value.", "The request body is not valid JSON."
  - The redundant "request body is required" error is dropped when a more precise one exists.
- [x] JWT Bearer security scheme in the OpenAPI document, and Scalar's preferred scheme. **Already present** in `Api/OpenApi/BearerSecurityTransformers.cs`:
  - The `Bearer` HTTP scheme (JWT) under `components.securitySchemes`.
  - A requirement on every operation except `[AllowAnonymous]` ones.
  - Scalar's `AddPreferredSecuritySchemes("Bearer")`.

  T11 added the tests for it.
- [x] Consistent 401 and 403 bodies: `JwtBearerEvents.OnChallenge` and `OnForbidden` write problem details.
  - 401 (`auth.unauthenticated`) carries `WWW-Authenticate: Bearer`, or `Bearer error="invalid_token"` for a bad or expired token.
  - 403 (`auth.forbidden`) covers authorization failures such as `AdminOnly`.
  - `UseStatusCodePages()` gives every other empty-body error (404, 405, 415) a problem body.
  - `CustomizeProblemDetails` adds a default `code` by status when the producer set none.
- [x] CORS policy `ui` (`Api/Security/CorsSettings.cs`):
  - Origins come from `Cors:AllowedOrigins` (defaults `http://localhost:4200` and `http://localhost:8080`).
  - Any header, `GET`/`POST`/`PUT`/`DELETE`, the `Location` header exposed, a 10-minute preflight cache, and **no credentials** (consistent with T09.1 and T12: the UI calls the API through a same-origin proxy).
  - Validated at startup: wildcards, paths, trailing slashes and non-HTTP schemes are rejected. Empty entries are ignored.
- [x] Health checks: `/health` (readiness, including SQL Server) and `/alive` (liveness) are now mapped **in every environment** (they were Development only), anonymously, for the Docker Compose health checks (T15). The bodies are status words only.
- [x] Options with validation at startup (`ValidateOnStart`): `JwtOptions` (T08), `CorsSettings` (new) and `DatabaseOptions` (was validated on first use, now at startup).
- [x] JSON: camelCase (default), **enums as strings** (`JsonStringEnumConverter` for controllers, minimal endpoints and the OpenAPI document), and `DateOnly` as `yyyy-MM-dd` (built in).
- [x] Pipeline order in `Program.cs`:
  1. `UseExceptionHandler`
  2. `UseStatusCodePages`
  3. API documentation
  4. HTTPS redirection
  5. `UseCors("ui")`
  6. `UseAuthentication`
  7. User telemetry
  8. `UseAuthorization`
  9. Endpoints

## Tests (TDD: 26 failed before the implementation)
- [x] `ExceptionHandlingTests`. A test-only controller is added to the API under test as an application part, and logs are captured with `FakeLoggerProvider`:
  - A code bug, a database exception and a wrapped database exception each return a 500 problem with `server.error` and `traceId`, and **no personal data, exception name or stack trace** in the body.
  - A code bug is logged **once**, with its exception.
  - Database exceptions are logged once, **without the exception and without the message**, and no log anywhere contains the personal data.
  - An escaped `DuplicateEntityException` returns 409 with `errors.heroName`. An escaped `DomainException` returns 400 with `errors.birthDate`.
- [x] `ProblemDetailsTests`. Every case checks problem+json, `status`, `title`, `traceId` and `code`:
  - A missing token returns 401 with the Bearer challenge. An invalid token returns 401 with `invalid_token`.
  - A regular user on `AdminOnly` gets a 403 problem. A signed-in caller on an unknown URL gets a 404 problem.
  - A body that isn't JSON gives `errors.body` = "The request body is not valid JSON." A wrong-type date gives `errors.birthDate` with no `System.` or `$.` in the body. An invalid query value gives `errors.sortBy`.
- [x] `CorsTests`:
  - Preflights from `localhost:4200` and `:8080` succeed (204, `Allow-Origin`, `authorization` header, `PUT`).
  - **An unknown origin gets no `Access-Control-Allow-Origin`.**
  - `Location` is exposed. Origins come from configuration.
  - Invalid origins (`*`, a trailing slash, no scheme, `ftp:`, a path) stop the API at startup.
- [x] `ConfigurationTests`: invalid `Database` settings stop startup; `/alive` and `/health` answer anonymously in **Production**; enums are strings for both JSON stacks.
- [x] `OpenApiAndScalarTests`: the `Bearer` scheme is declared; protected operations (`GET/POST /api/heroes`, `PUT /api/users/{id}`, `GET /api/auth/me`) require it; anonymous ones (login, register, `/api/v1/test`) don't.

## Done when
- [x] Every error response from the API follows the problem details format (use cases, auth, routing, binding, exceptions).
- [x] All tests pass: Domain 126, Application 187, Infrastructure 252, ServiceDefaults 20 and Api 114, for **699** in total. The Release build has 0 warnings.
- [x] **Live check under Aspire:**
  1. `/health` returns `Healthy`.
  2. An anonymous `/api/heroes` gets a 401 problem with `traceId` and `code`.
  3. `Admin` / `Admin` logs in.
  4. Searching "flight" finds Superman.
  5. Malformed JSON gets a 400 with `errors.body`.
  6. A preflight from `localhost:4200` gets `Allow-Origin`; one from `evil.example` gets none.
  7. The Angular dev proxy (`localhost:4200/api/...`) reaches the API (200).
