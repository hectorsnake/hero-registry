# T09: Auth and Users API

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** API · **Estimate:** 4 h · **Depends on:** T06, T08

## Goal
Expose the second API for user creation, login and user management, with both authorized and anonymous endpoints.

## Steps
- [ ] Configure JWT Bearer authentication from `JwtOptions`. Add an authorization policy `AdminOnly` (`role = Admin`). Set a fallback policy that requires an authenticated user.
  - Add `Microsoft.AspNetCore.Authentication.JwtBearer` **10.0.15**. It uses IdentityModel 8.19.2, the same version as the T08 token service.
  - Validate with the same `JwtOptions` (registered and validated at startup by T08): `ValidIssuer`, `ValidAudience`, `IssuerSigningKey` (UTF-8 bytes of `Key`), lifetime checked, and a small `ClockSkew` (for example 30 s).
  - Keep the claim names the T08 tokens use: `MapInboundClaims = false`, `NameClaimType = "unique_name"`, `RoleClaimType = "role"`. Then `User.IsInRole(Roles.Admin)` and `HttpCurrentUser` (which reads `sub`) work as they are.
  - Use cases are ready: `IAuthService` (`RegisterAsync`, `LoginAsync`) and `IUserService` (`GetCurrentAsync`, `ListAsync`, `DeleteAsync`), with self-delete already blocked as a Validation error on `Id`.
  - Tests: `HeroRegistryApiFactory.TestJwtKey` is the key of the API under test.
- [x] `HttpCurrentUser : ICurrentUser` reads the claims from `IHttpContextAccessor`. **Done in T07** (`Api/Security/HttpCurrentUser.cs`, with `Roles.Admin` and `Roles.User`). The JWT from T08 must therefore put the user id in `sub` and the role in the role claim, matching `Roles`.
- [ ] Telemetry ([T02.2](T02.2-Application-Insights-Logging.md)): tag request spans with the user id (`enduser.id`), never the user name or email, so Application Insights can filter requests by user.
- [ ] `AuthController` (`/api/auth`):
  - `POST register` (`[AllowAnonymous]`): returns 201 + `UserResponse`, 400 or 409.
  - `POST login` (`[AllowAnonymous]`): returns 200 + `AuthResponse` or 401.
  - `GET me` (authorized): returns 200 + `UserResponse`.
- [ ] `UsersController` (`/api/users`, `[Authorize(Policy = "AdminOnly")]`):
  - `GET`: returns 200 + the list.
  - `DELETE {id:int}`: returns 204, 404 or 400 (cannot delete yourself).
- [ ] Map `Result` errors to ProblemDetails in one place (a base controller or `ToActionResult()` extension). `ErrorType` (T07) maps to status codes: Validation 400 (`ValidationProblemDetails` from `FieldErrors`), Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409 (with `FieldErrors` when present). Include `Error.Code` as a ProblemDetails extension.
- [ ] Add `[ProducesResponseType]` attributes so the OpenAPI document is accurate.
- [ ] Add request samples to `HeroRegistry.Api.http`.

## Tests (TDD, `WebApplicationFactory` with Testcontainers SQL Server or a fake repository)
- [ ] Register returns 201, and registering the same user again returns 409. Invalid input returns 400 with an `errors` dictionary.
- [ ] Login with the seeded `Admin` / `Admin` returns 200 with a token that has the `Admin` role, and a newly registered user gets the `User` role. A bad password returns 401.
- [ ] `GET /api/auth/me` returns 401 without a token and 200 with one.
- [ ] `GET /api/users` returns 403 for a regular user (registered by the test) and 200 for `Admin`.

## Done when
- All endpoints behave as in the README's endpoint tables, and the API tests pass.
