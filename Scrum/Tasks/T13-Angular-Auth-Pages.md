# T13: Angular login and register pages (Signal Forms)

**User story:** [US-001](../US-001-SuperHero-Registry.md) (AC1, AC2, AC12) · **Layer:** UI · **Estimate:** 4 h · **Depends on:** T12 · **Status:** Done

## Goal
Let visitors create an account and log in, using **Signal Forms** and the signals-first conventions from [T12](T12-Angular-Core.md): zoneless, OnPush, `inject()`, `input()`/`output()`, `@if`/`@for`, and no `.subscribe()` in components.

## Signal Forms in this task
APIs verified against the installed `@angular/forms/signals` (Angular 22.3):
- **Model:** a `signal<LoginModel>({ userName: '', password: '' })`. The form is `form(model, loginSchema)`, and the model signal stays the single source of truth.
- **Schema:** a reusable `schema<T>(path => { ... })` with the built-in rules `required`, `email`, `pattern`, `minLength` and `maxLength`. `validate` handles custom rules, and `validateTree` handles cross-field rules (confirm password).
- **Template:**
  - `<form [formRoot]="loginForm">`, with `<input [formField]="loginForm.userName">` on each input.
  - Field state comes from signals: `loginForm.userName().invalid()`, `touched()` and `errors()`. The submit button uses `loginForm().invalid()` and the pending state.
- **Submit:** `submit(loginForm, async () => { ... })` marks every field as touched, runs only when the form is valid, and is the one place where server errors are mapped back onto the fields.

## TDD (mandatory)
Follow the [T12 TDD workflow](T12-Angular-Core.md#tdd-workflow-mandatory-for-t12-t13-and-t14): red → green → refactor, one behavior at a time, with evidence in the Implementation notes. Suggested order, each driven by its specs from the Tests section:
1. `auth.schemas.ts` (pure, fastest feedback): boundaries for user name, email, every password rule, and confirm password.
2. `FormFieldErrorComponent`.
3. `LoginComponent`: empty form invalid → submit payload → navigation → 401 message → open-redirect guard → pending state.
4. `RegisterComponent`: password checklist → server 409/400 mapping → error cleared on edit → login and navigate.

## Steps
- [x] **Shared schemas (`features/auth/auth.schemas.ts`)**, mirroring the server rules (US-001, User table):
  - `userNameSchema`: `required`, `minLength(3)`, `maxLength(50)`, `pattern(/^[a-zA-Z0-9._-]+$/)`.
  - `emailSchema`: `required`, `email`.
  - `passwordPolicySchema`: `required`, `minLength(8)`, and a `validate` rule for uppercase, lowercase, digit and symbol. It returns one error per missing rule (kind `password.policy`, with a message), so the UI can show a checklist.
  - Typed error messages live in one map (`kind → message`), so no template hard-codes text.
- [x] **`LoginComponent` (`/login`):**
  - `model = signal({ userName: '', password: '' })` and `loginForm = form(this.model, s => { required(s.userName); required(s.password); })`.
  - `returnUrl = input<string>()`, bound from the query string by `withComponentInputBinding()` (T12). No `ActivatedRoute` subscription.
  - `pending = signal(false)` and `serverError = signal<string | null>(null)`. The submit button is disabled while `pending()` is true, and shows a spinner with `@if (pending())`.
  - Submit through `submit(loginForm, ...)`:
    - Calls `AuthService.login()` (bridged with `firstValueFrom`).
    - A 401 (`auth.invalid_credentials`) shows the generic "Invalid user name or password" in an `aria-live` region. It never reveals which field was wrong.
    - On success, `router.navigateByUrl(returnUrl() ?? '/heroes')`. Reject external `returnUrl` values (it must start with `/` and not `//`) to prevent open redirects.
  - If the user is already signed in (`auth.isAuthenticated()`), a guard redirects from `/login` to `/heroes`.
  - Shows the "session expired" message when T12's `refreshInterceptor` redirected here (a `reason=expired` query input).
- [x] **`RegisterComponent` (`/register`):**
  - `model = signal({ userName: '', email: '', password: '', confirmPassword: '' })`.
  - The schema applies `userNameSchema`, `emailSchema` and `passwordPolicySchema` (with `apply`), plus a `validateTree` rule: `confirmPassword` must equal `password` (error on `confirmPassword`).
  - A **live password checklist** (`computed` from `registerForm.password().errors()`) with a ✓ or ✗ per rule, updated as the user types.
  - Submit through `submit(registerForm, ...)`:
    - `AuthService.register()`, then automatic login, then navigate to `/heroes`.
    - **400 / 409** map onto the fields with T12's `toFieldErrors(problem)`, for example `errors.userName` → the `userName` field ("This user name is already taken"), returned from the submit action so Signal Forms attaches them to the right field. Other problems show the ProblemDetails `title`.
    - Server errors on a field clear as soon as the user edits that field.
- [x] **Shared `FormFieldErrorComponent`** (`shared/forms`):
  - `field = input.required<FieldTree<unknown>>()`.
  - Shows the first error of `field().errors()` only when `touched()` is true, using the message map.
  - Sets `id` for `aria-describedby`, and the input gets `aria-invalid` from `field().invalid()`.
- [x] **Accessibility:**
  - Every input has a `<label>`, and the attributes `autocomplete="username"`, `"email"`, `"current-password"` or `"new-password"`.
  - Enter submits the form.
  - After a failed submit, focus moves to the first invalid field (an `effect`-free `viewChildren` lookup in the submit handler).
  - Color contrast is AA.
- [x] **Show and hide password** toggle with a `showPassword = signal(false)` and `aria-pressed`.
- [x] **Demo hint** on the login page ("Try Admin / Admin"), shown only in non-production builds (`isDevMode()`).
- [x] **Responsive:** single-column cards, usable at 360 px.

## Tests (TDD, Vitest, zoneless TestBed)
Assert against the form's signals directly (`form.userName().errors()`). Use `await fixture.whenStable()` after typing, with no `fakeAsync`.

- [x] **Schemas:**
  - User name pattern and length boundaries (2, 3, 50 and 51 characters).
  - Email format.
  - Each password rule fails on its own. `confirmPassword` mismatch puts the error on `confirmPassword`.
- [x] **`LoginComponent`:**
  - The form is invalid while empty and the button is disabled.
  - Submit calls `AuthService.login` with the payload, and navigates to `returnUrl` or `/heroes`.
  - A 401 shows the generic message.
  - An external `returnUrl` (`https://evil.com`, `//evil.com`) is ignored.
  - The button is disabled while the request is pending.
- [x] **`RegisterComponent`:**
  - The password checklist reacts to typing.
  - A server 409 with `errors.userName` puts the error on the `userName` field, and editing the field clears it.
  - A 400 maps every returned field.
  - Success logs in and navigates.
- [x] **`FormFieldErrorComponent`:** hidden until the field is touched, then shows the mapped message.

## Done when
- A new user can register and log in from the UI at 360 px and on desktop, with field-level server errors and no console warnings.
- No Reactive Forms or template-driven forms are used. ESLint (T12 rules) passes.
- **TDD evidence:** every spec listed in Tests was red before its implementation (recorded in the Implementation notes). `npm run test:coverage` meets 80% lines and branches for `features/auth` and `shared/forms`.

## Implementation notes

### TDD evidence (red → green)
Each spec was written first and run on its own (`npm test -- --watch=false --include=<path>`) before the code existed. "Compile" means the build failed because the unit did not exist yet (`TS2307` / `TS2305`).

| # | Unit | Specs | Red run | Green |
| - | ---- | ----- | ------- | ----- |
| 1 | `features/auth/auth-schemas` (user name, email and password policy, confirm password, checklist, message map) | 36 | 36 failed (compile) | 36/36 |
| 2 | `shared/forms/FormFieldError` | 5 | 5 failed (compile) | 5/5 |
| 3 | `guestGuard` (T12 guards) | +2 | failed (compile: `guestGuard` not exported) | 7/7 |
| 4 | `refreshInterceptor` sends `reason=expired` (T12) | 1 changed | 1 failed: `expected "navigate" to be called with arguments [['/login'], …]` | 10/10 |
| 5 | `Login` | 19 | 18 failed against the T12 placeholder (`Cannot read properties of null`). 1 passed: "says nothing about sessions on a normal visit" is a negative check, made meaningful by the "explains an expired session" spec, which was red | 19/19 (see the spec fix below) |
| 6 | `Register` | 10 | 10 failed (compile) | 10/10 (see the spec fix below) |
| 7 | Submit-on-invalid (found in the smoke test, see Decisions) | 3 changed | Login: 2 failed (`expected <body> to be <input>`, `expected true to be false`). Register: 1 failed (focus) | 65/65 in `features/auth` |

Final run: **18 files, 188 tests passed**. Coverage of `core`, `features`, `shared` and `layout`: 98.5% lines and 94.9% branches. The threshold is 80% for both. `npm run lint` and `npm run build` are clean, Prettier is clean, and no Reactive or template-driven forms are used.

### Spec corrections (the spec was wrong, not the code)
- **Login, pending state:** `whenStable()` does not wait for the submit action's promise chain. The spec now waits for the navigation before asserting that the button is enabled again.
- **Register, 400 mapping:** the password input's `aria-describedby` lists two ids (its error, then the checklist), and the spec helper used both as one selector. It now takes the first id.
- `safeReturnUrl` and `focusFirstInvalid` have no spec of their own. They were driven red-first through the Login and Register specs: four external `returnUrl` values, and focus after an invalid submit.

### Decisions and deviations from the steps
- **The submit button is disabled only while pending, not while the form is invalid.**
  - The smoke test showed that a disabled button silently drops a fast Enter. Implicit submission checks the button before the zoneless re-render enables it, and password-manager autofill-and-submit hits the same problem.
  - Instead, an invalid submit marks every field as touched, shows all errors and focuses the first invalid field (`onInvalid` → `focusBoundControl()`, no `viewChildren` needed). This also covers the "focus the first invalid field after a failed submit" step.
  - The spec "invalid while empty, with the button disabled" was replaced by "submitting while empty shows every error and focuses the first field, without calling the API".
- **`pending` is `computed(() => form().submitting())`** instead of a hand-managed `signal(false)`. Signal Forms already tracks it, and it also blocks a second submit.
- **No `firstValueFrom`:** `AuthService.login()` and `register()` already return promises (T12).
- **Server errors** are returned from the `submission.action` as `{ kind: 'server', message, fieldTree }`. Signal Forms attaches them to the field and clears them as soon as the value changes. A problem without field errors shows its `title`. 0 and 5xx are left to the T12 `errorInterceptor` toast.
- **Login failures:** any 400 or 401 on login shows only the generic "Invalid user name or password.". No field is marked, so the message never reveals which field was wrong.
- **Expired session:** the `refreshInterceptor` now adds `reason=expired` and no longer shows a toast. The login page shows "Your session has expired. Please sign in again.".
- **`guestGuard`** (in `core/auth/guards.ts`) keeps signed-in users away from `/login` and `/register`.
- **File names** follow the Angular style guide used in T12: `auth-schemas.ts`, `form-field-error.ts`.
- **Messages:** `FormFieldError` (in `shared`) shows the error's own message, because `shared` must not import a feature. Every auth rule passes its message from `AUTH_MESSAGES`, which is the single message map, and `errorMessage()` resolves a message by kind for errors that have none.

### Bug fix after delivery: "Your session has expired" right after signing in (Aspire only)
- **Symptom:** under Aspire, signing in immediately showed `/login?returnUrl=%2Fheroes&reason=expired`.
- **Cause:**
  - `proxy.conf.mjs` preferred the API's **http** endpoint. Under Aspire the API also has an https endpoint, so `UseHttpsRedirection` answered every proxied call with `307 → https://localhost:7227/...`.
  - The browser followed the redirect to another origin. There it ignored the refresh cookie's `Set-Cookie` (a cross-origin response without credentials), and it dropped the `Authorization` header on the cross-origin redirect.
  - So `GET /api/auth/me` returned 401, the refresh had no cookie (`auth.refresh_invalid`), and the refresh interceptor sent the user to the login page.
  - The T12 and T13 smoke tests missed it because the API ran on its own there, with no https port, so it never redirected.
- **Fix:** the proxy prefers `services__api__https__0` (with `secure: false` for the dev certificate) and falls back to http. The browser now only ever talks to `localhost:4200`.
- **Verified** against the running Aspire API through a second dev server on port 4300: login returned 200 with `Set-Cookie: hr_refresh` (HttpOnly, Secure, SameSite=Strict, `Path=/api/auth`), with no redirect. `/me` returned 200, refresh 200 and logout 204.

### Smoke test (Chrome, real API through the dev proxy)
- **Register (desktop):**
  - The checklist updated live while typing.
  - The account `smoke.t13` was created, signed in automatically, and landed on `/heroes` with the user in the navbar.
  - The first Enter was lost while the button was still disabled, which led to the decision above.
- **Login:**
  - An empty submit showed "This field is required." on both fields, set `aria-invalid="true"` and focused User name.
  - A wrong password showed the generic message.
  - The correct password plus an immediate Enter signed in at the first attempt, after the fix.
- **`guestGuard`:** `/login` while signed in redirected to `/heroes`.
- **360 px:** checked by rendering both pages in a 360 px same-origin iframe, because the browser window could not be resized. No horizontal overflow on either page (`scrollWidth` 360, card 328 px), and the submit button was visible.
- **Console:** no warnings. The only error was the known dev-only view-transition message from T12, logged while the tab was hidden.
- **Test data left behind:** the user `smoke.t13` remains in the development database.
- **Chrome autofill:** Chrome filled a saved login for `localhost:4200` (left by another app) into the form, and one click submitted it to the local API (401). Chrome, not the app, chose those values. Developers testing locally may see the same.
