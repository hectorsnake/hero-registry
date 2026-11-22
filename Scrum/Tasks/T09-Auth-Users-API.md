# T09: Auth and Users API

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** API · **Estimate:** 4 h · **Depends on:** T06, T08

## Goal
Expose the second API for user creation, login and user management, with both authorized and anonymous endpoints.

## Steps
- [ ] Configure JWT Bearer authentication from `JwtOptions`. Add an authorization policy `AdminOnly` (`role = Admin`). Set a fallback policy that requires an authenticated user.
- [ ] `HttpCurrentUser : ICurrentUser` reads the claims from `IHttpContextAccessor`.
- [ ] `AuthController` (`/api/auth`):
  - `POST register` (`[AllowAnonymous]`): returns 201 + `UserResponse`, 400 or 409.
  - `POST login` (`[AllowAnonymous]`): returns 200 + `AuthResponse` or 401.
  - `GET me` (authorized): returns 200 + `UserResponse`.
- [ ] `UsersController` (`/api/users`, `[Authorize(Policy = "AdminOnly")]`):
  - `GET`: returns 200 + the list.
  - `DELETE {id:int}`: returns 204, 404 or 400 (cannot delete yourself).
- [ ] Map `Result` errors to ProblemDetails in one place (a base controller or `ToActionResult()` extension).
- [ ] Add `[ProducesResponseType]` attributes so the OpenAPI document is accurate.
- [ ] Add request samples to `HeroRegistry.Api.http`.

## Tests (TDD, `WebApplicationFactory` with Testcontainers SQL Server or a fake repository)
- [ ] Register returns 201, and registering the same user again returns 409. Invalid input returns 400 with an `errors` dictionary.
- [ ] Login with the seeded `demo` user returns 200 with a token. A bad password returns 401.
- [ ] `GET /api/auth/me` returns 401 without a token and 200 with one.
- [ ] `GET /api/users` returns 403 for `demo` and 200 for `admin`.

## Done when
- All endpoints behave as in the README's endpoint tables, and the API tests pass.
