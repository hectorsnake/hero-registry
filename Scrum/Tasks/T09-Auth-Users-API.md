# T09: Auth and Users API

**User story:** [US-001](../US-001-SuperHero-Registry.md) (AC1, AC2, AC3, **AC11**) · **Layer:** Domain, Application and API · **Estimate:** 6 h (4 h API + 2 h account update) · **Depends on:** T06, T08 · **Status:** Done

## Goal
Expose the second API for user creation, login and user management, with both authorized and anonymous endpoints. It also includes **account update** (AC11): users update their own account, and administrators can update any account.

## Steps
- [x] **JWT Bearer authentication** (`Api/Security/AuthenticationExtensions.cs`, `AddJwtAuthentication()`):
  - `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15, the same IdentityModel 8.19.2 as the T08 token service.
  - Validates with the same `JwtOptions`: issuer, audience, signing key, lifetime (30 s clock skew), and only `HS256` accepted.
  - `MapInboundClaims = false`, `NameClaimType = "unique_name"`, `RoleClaimType = "role"`, so `HttpCurrentUser` and `IsInRole(Roles.Admin)` read the claims exactly as issued.
  - Policies: `AdminOnly` (role `Admin`), plus a **fallback policy that requires an authenticated user**: secure by default, so a new endpoint without attributes is protected.
  - Opted out with `AllowAnonymous`: register, login, `/health`, `/alive`, `/api/v1/test`, and the OpenAPI document, Scalar and the `/` redirect (all Development only).
- [x] `HttpCurrentUser : ICurrentUser` (done in T07).
- [x] Telemetry: `UseUserTelemetry()` (after `UseAuthentication`) tags the request span with `enduser.id` (the user id only, never the name or email).
- [x] `AuthController` (`/api/auth`):
  - `POST register` (`[AllowAnonymous]`): 201 + `UserResponse` (no Location header, since accounts are read via `/me` or `/api/users`), 400 or 409.
  - `POST login` (`[AllowAnonymous]`): 200 + `AuthResponse` (`accessToken`, `tokenType`, `expiresAt`, `user`), 400 or 401.
  - `GET me`: 200 + `UserResponse`, or 401.
- [x] **Account update (AC11): use case**:
  - Domain: `User.UpdateAccount(userName, email, updatedBy, utcNow)` validates and normalizes, sets the update audit, rejects deleted accounts, and never touches `Password`, `IsAdmin` or the creation audit.
  - Application: `UpdateUserRequest` (`UserName`, `Email`), `UserRequestValidator.Validate(UpdateUserRequest)`, and `IUserRepository.UpdateAsync` (implemented by the base repository).
  - `IUserService.UpdateAsync(id, request)`. The order of checks:
    1. Not signed in: 401.
    2. **Not the owner and not an admin: 403, before any lookup.**
    3. Invalid input: 400.
    4. Missing account: 404.
    5. A user name or email of **another** account: 409. Keeping your own values is allowed. A database duplicate is also 409.
    6. Saved: 200. Deleted meanwhile: 404.
- [x] `UsersController` (`/api/users`, `[Authorize]` on the controller):
  - `GET` (`AdminOnly`): 200 + the list, 401 or 403.
  - **`PUT {id:int}`** (any signed-in user; the owner-or-admin rule is resource-based and lives in the use case): 200, 400, 401, 403, 404 or 409.
  - `DELETE {id:int}` (`AdminOnly`): 204, 400 (cannot delete yourself), 401, 403 or 404.
- [x] **`Result` errors become problem details in one place** (`Api/Controllers/ApiControllerBase.cs`):
  - Status codes: Validation 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409.
  - `ValidationProblemDetails` with an `errors` dictionary (**camelCase keys**, like the JSON properties) whenever there are field errors, for both 400 and 409.
  - Every problem carries a `code` extension (`user.duplicate`, `auth.invalid_credentials`, ...). Content type: `application/problem+json`.
  - Helpers: `OkOrError` and `NoContentOrError`.
- [x] `[ProducesResponseType]` on every action, so the OpenAPI document lists every status code.
- [x] `HeroRegistry.Api.http`: log in as Admin (the token is captured), register, me, list, update and delete.

## Tests (TDD, `WebApplicationFactory` with Testcontainers SQL Server)
- [x] **Test infrastructure:**
  - A SQL Server container per test run (`Api.Tests/Fixtures/SqlServerContainerFixture`, started lazily).
  - `DatabaseApiFactory`: a fresh database per test class, which **the API itself creates, migrates and seeds at startup**, so login uses the real seeded Admin.
  - `ApiClientExtensions` helpers: register, log in, a signed-in client, and test tokens without a database.
- [x] `AuthEndpointsTests`:
  - Register returns 201 (normalized email, no password field). The same user again returns 409 (`user.duplicate`, `errors.userName`, problem+json). Invalid input returns 400 with every field in `errors`, without echoing the password.
  - Login as `Admin` / `Admin` returns 200 with an `Admin` role token and `tokenType` `Bearer`. A new user gets the `User` role. A wrong password and an unknown user return the **same** 401 (`auth.invalid_credentials`). Missing fields return 400.
  - `GET /api/auth/me`: 401 without a token (with `WWW-Authenticate: Bearer`), 200 with one, 401 for a garbage or forged token.
  - `/health` stays anonymous while `/api/users` requires a token.
- [x] `UsersEndpointsTests`:
  - `GET /api/users`: 403 for a regular user, 200 for `Admin`.
  - **AC11:**
    - Own update returns 200, and the user then logs in with the **new name and the same password**.
    - **Another account as a regular user: 403. A non-existent id as a regular user: 403, not 404.**
    - Admin updates another account: 200. Anonymous: 401. Unknown id as Admin: 404. Another account's user name: 409. Invalid email: 400.
  - Delete: an admin deletes a user (204), who then can't log in (401). A regular user gets 403. Self-delete returns 400 (`errors.id`). Unknown id returns 404.
- [x] Domain (`UserTests`) and Application (`UserServiceTests`, `UpdateUserRequestValidatorTests`): every AC11 rule with mocks, including **Forbidden with no repository calls for any foreign id**.
- [x] Telemetry: a signed-in request's span has `enduser.id = "7"` and never the user name; an anonymous request has no user tag.
- [x] **Red check:** disabling the owner-or-admin check makes exactly the two AC11 security tests fail.
- [x] Existing tests adjusted for the fallback policy: an unknown URL is **401 for anonymous callers** (no route probing) and **404 for signed-in callers**. The Production "API reference not exposed" test now signs in.

## Done when
- [x] All endpoints behave as in the README's endpoint tables, including `PUT /api/users/{id}`.
- [x] All tests pass: Domain 126, Application 187, Infrastructure 252, ServiceDefaults 20 and Api 55, for **640** in total. The Release build has 0 warnings.

## Implementation notes
- **401 and 403 from the authentication middleware** (no token, wrong role on `AdminOnly`) have an empty body. 401 and 403 from the use cases (for example AC11's "not your account") are problem details. T11 can make both consistent.
- **Renaming yourself:** the token stays valid (it identifies the user by `sub`, the id). The `unique_name` claim shows the old name until the next login.
