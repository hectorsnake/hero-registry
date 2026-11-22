# T13: Angular login and register pages

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** UI · **Estimate:** 4 h · **Depends on:** T12

## Goal
Let visitors create an account and log in.

## Steps
- [ ] `LoginComponent` (`/login`):
  - Reactive form with user name and password, both required.
  - Submit button disabled while the request is pending.
  - On a 401 response, shows a generic "Invalid credentials" message.
  - On success, redirects to `returnUrl` or `/heroes`.
- [ ] `RegisterComponent` (`/register`):
  - Fields: user name, email, password and confirm password.
  - Validators mirror the server rules: user name pattern, email, the password policy, and passwords must match.
  - Shows server 409 and 400 errors next to the relevant fields.
  - On success, logs in automatically (or redirects to login with a success message).
- [ ] Shared `FormFieldErrorComponent` to render validation messages consistently.
- [ ] Accessibility: labels, `autocomplete` attributes and keyboard submit.
- [ ] Demo hint on the login page ("Try Admin / Admin"), shown only in non-production builds.

## Tests (TDD)
- [ ] The form is invalid while empty. Password mismatch and weak passwords produce errors.
- [ ] Submit calls `AuthService` with the correct payload, and navigates on success.
- [ ] Server error mapping sets the error on the correct form control.

## Done when
- A new user can register and log in from the UI on both mobile and desktop widths.
