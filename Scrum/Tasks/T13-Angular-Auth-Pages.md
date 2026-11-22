# T13: Angular login and register pages (Signal Forms)

**User story:** [US-001](../US-001-SuperHero-Registry.md) (AC1, AC2, AC12) · **Layer:** UI · **Estimate:** 4 h · **Depends on:** T12

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

## Steps
- [ ] **Shared schemas (`features/auth/auth.schemas.ts`)**, mirroring the server rules (US-001, User table):
  - `userNameSchema`: `required`, `minLength(3)`, `maxLength(50)`, `pattern(/^[a-zA-Z0-9._-]+$/)`.
  - `emailSchema`: `required`, `email`.
  - `passwordPolicySchema`: `required`, `minLength(8)`, and a `validate` rule for uppercase, lowercase, digit and symbol. It returns one error per missing rule (kind `password.policy`, with a message), so the UI can show a checklist.
  - Typed error messages live in one map (`kind → message`), so no template hard-codes text.
- [ ] **`LoginComponent` (`/login`):**
  - `model = signal({ userName: '', password: '' })` and `loginForm = form(this.model, s => { required(s.userName); required(s.password); })`.
  - `returnUrl = input<string>()`, bound from the query string by `withComponentInputBinding()` (T12). No `ActivatedRoute` subscription.
  - `pending = signal(false)` and `serverError = signal<string | null>(null)`. The submit button is disabled while `pending()` is true, and shows a spinner with `@if (pending())`.
  - Submit through `submit(loginForm, ...)`:
    - Calls `AuthService.login()` (bridged with `firstValueFrom`).
    - A 401 (`auth.invalid_credentials`) shows the generic "Invalid user name or password" in an `aria-live` region. It never reveals which field was wrong.
    - On success, `router.navigateByUrl(returnUrl() ?? '/heroes')`. Reject external `returnUrl` values (it must start with `/` and not `//`) to prevent open redirects.
  - If the user is already signed in (`auth.isAuthenticated()`), a guard redirects from `/login` to `/heroes`.
  - Shows the "session expired" message when T12's `refreshInterceptor` redirected here (a `reason=expired` query input).
- [ ] **`RegisterComponent` (`/register`):**
  - `model = signal({ userName: '', email: '', password: '', confirmPassword: '' })`.
  - The schema applies `userNameSchema`, `emailSchema` and `passwordPolicySchema` (with `apply`), plus a `validateTree` rule: `confirmPassword` must equal `password` (error on `confirmPassword`).
  - A **live password checklist** (`computed` from `registerForm.password().errors()`) with a ✓ or ✗ per rule, updated as the user types.
  - Submit through `submit(registerForm, ...)`:
    - `AuthService.register()`, then automatic login, then navigate to `/heroes`.
    - **400 / 409** map onto the fields with T12's `toFieldErrors(problem)`, for example `errors.userName` → the `userName` field ("This user name is already taken"), returned from the submit action so Signal Forms attaches them to the right field. Other problems show the ProblemDetails `title`.
    - Server errors on a field clear as soon as the user edits that field.
- [ ] **Shared `FormFieldErrorComponent`** (`shared/forms`):
  - `field = input.required<FieldTree<unknown>>()`.
  - Shows the first error of `field().errors()` only when `touched()` is true, using the message map.
  - Sets `id` for `aria-describedby`, and the input gets `aria-invalid` from `field().invalid()`.
- [ ] **Accessibility:**
  - Every input has a `<label>`, and the attributes `autocomplete="username"`, `"email"`, `"current-password"` or `"new-password"`.
  - Enter submits the form.
  - After a failed submit, focus moves to the first invalid field (an `effect`-free `viewChildren` lookup in the submit handler).
  - Color contrast is AA.
- [ ] **Show and hide password** toggle with a `showPassword = signal(false)` and `aria-pressed`.
- [ ] **Demo hint** on the login page ("Try Admin / Admin"), shown only in non-production builds (`isDevMode()`).
- [ ] **Responsive:** single-column cards, usable at 360 px.

## Tests (TDD, Vitest, zoneless TestBed)
Assert against the form's signals directly (`form.userName().errors()`). Use `await fixture.whenStable()` after typing, with no `fakeAsync`.

- [ ] **Schemas:**
  - User name pattern and length boundaries (2, 3, 50 and 51 characters).
  - Email format.
  - Each password rule fails on its own. `confirmPassword` mismatch puts the error on `confirmPassword`.
- [ ] **`LoginComponent`:**
  - The form is invalid while empty and the button is disabled.
  - Submit calls `AuthService.login` with the payload, and navigates to `returnUrl` or `/heroes`.
  - A 401 shows the generic message.
  - An external `returnUrl` (`https://evil.com`, `//evil.com`) is ignored.
  - The button is disabled while the request is pending.
- [ ] **`RegisterComponent`:**
  - The password checklist reacts to typing.
  - A server 409 with `errors.userName` puts the error on the `userName` field, and editing the field clears it.
  - A 400 maps every returned field.
  - Success logs in and navigates.
- [ ] **`FormFieldErrorComponent`:** hidden until the field is touched, then shows the mapped message.

## Done when
- A new user can register and log in from the UI at 360 px and on desktop, with field-level server errors and no console warnings.
- No Reactive Forms or template-driven forms are used. ESLint (T12 rules) passes.
