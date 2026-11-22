# T08: User registration and login use cases (hashing and JWT)

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Application and Infrastructure · **Estimate:** 5 h · **Depends on:** T04

## Goal
Implement user sign-up and login: business rules in Application, and cryptography and token adapters in Infrastructure.

## Steps
- [ ] Application:
  - DTOs: `RegisterUserRequest`, `LoginRequest`, `AuthResponse` (token, expiresAt, user), `UserResponse`
  - Ports: `IPasswordHasher` (`Hash`, `Verify`) and `ITokenService` (`CreateToken(User)`)
  - Validators for user name, email and the password policy
  - `AuthService.RegisterAsync`: validate, check uniqueness (Conflict), hash the password, `User.Register`, save. `IsAdmin` is always `false`.
  - `AuthService.LoginAsync`: look up the user by user name (case-insensitive) and verify the password. A missing user, a deleted user and a wrong password all return the **same** `Unauthorized` error. Hash a dummy password when the user is missing, to resist timing attacks.
  - `UserService.ListAsync` and `UserService.DeleteAsync` (admin only; an admin cannot delete themselves).
- [ ] Infrastructure:
  - `BCryptPasswordHasher` (BCrypt.Net-Next 4.2.0, already pinned in `Directory.Packages.props`, work factor 11 or higher). It must verify the seeded administrator's `$2a$11$` hash from `0003_SeedData.sql` (T03.1). Add a test that `Verify("Admin", seededHash)` succeeds.
  - `JwtTokenService`: claims `sub` (user id), `unique_name` and `role` (`Admin` or `User`). HMAC-SHA256, expiration from `JwtOptions` (issuer, audience, key, minutes).
  - Validate `JwtOptions` on start (`ValidateOnStart`); the key must be at least 32 bytes.

## Tests (TDD)
- [ ] `AuthService`: registration success hashes the password, so the stored value is not the plain text. Covers duplicate user name or email and weak passwords. Login success returns a token. Wrong password, unknown user and deleted user all return identical errors.
- [ ] `BCryptPasswordHasher`: a hash verifies, and the wrong password fails.
- [ ] `JwtTokenService`: a token can be decoded and contains the expected claims and expiration.

## Done when
- No plain-text password is ever persisted or logged, and all auth tests pass.
