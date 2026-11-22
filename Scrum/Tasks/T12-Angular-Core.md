# T12: Angular core (signals-first: auth, interceptors, guards and layout)

**User story:** [US-001](../US-001-SuperHero-Registry.md) (AC9, AC12) · **Layer:** UI (`hero-registry-ui`) · **Estimate:** 7 h · **Depends on:** T09, T09.1

## Goal
Create the base structure of the Angular 22 app as a **signals-first, zoneless** application: a clean folder layout, signal-based state, HTTP plumbing and a responsive shell. This task also sets the conventions that T13 and T14 follow.

Token handling follows current best practice for browser apps (OWASP, OAuth for browser-based apps):
- The **access token lives in memory only** (a signal). It is never written to `localStorage`, `sessionStorage` or a readable cookie.
- The **refresh token is an HttpOnly cookie** that the API manages (T09.1). JavaScript never sees it.
- Expired access tokens are **refreshed silently**. The user is only sent to login when the refresh itself fails.

## Modern Angular conventions (apply to T12, T13 and T14)
Verified against the installed Angular 22.3 packages. The APIs below are stable (`@publicApi`).

| Topic | Use | Avoid |
| ----- | --- | ----- |
| Change detection | **Zoneless** (no `zone.js` in `package.json`, the Angular 22 default) and **OnPush** (the Angular 22 default for components) | Adding `zone.js`, `ChangeDetectionStrategy.Eager`, `ChangeDetectorRef.detectChanges()` |
| Components | Standalone (the default), `inject()`, `host: {}` metadata | NgModules, constructor injection, `@HostBinding` / `@HostListener` |
| Component API | `input()`, `input.required()`, `output()`, `model()`, `viewChild()`, `contentChild()` | `@Input`, `@Output`, `@ViewChild`, `ngOnChanges` |
| Local and shared state | `signal()`, `computed()`, `linkedSignal()` (state that resets when a source changes, for example page → 1 when the search changes) | `BehaviorSubject` stores, mutable fields read by templates |
| Side effects | `effect()` only to push signal state to things outside Angular (focus, scroll, third-party widgets) | `effect()` that copies one signal into another (use `computed` or `linkedSignal`) |
| Reading data | `httpResource()` / `resource()`: `value()`, `isLoading()`, `error()`, `reload()`, with signals as reactive parameters | Manual `subscribe()` + loading flags in components |
| Writing data | `HttpClient` in a feature service. After a write, call the resource's `reload()` | Optimistic copies of server state scattered across components |
| RxJS | Only where streams fit: interceptors, the shared in-flight token refresh, debounce. Bridge with `toSignal()` / `toObservable()` | `.subscribe()` in components, the `async` pipe |
| Templates | `@if`, `@for` (with `track`), `@switch`, `@defer` for heavy parts; `[class.x]` / `[style.x]` bindings | `*ngIf`, `*ngFor`, `ngClass`, `ngStyle` |
| Forms | **Signal Forms** (`@angular/forms/signals`: `form()`, `schema()` with `required`/`email`/`pattern`/`validate`..., `[formField]` on inputs, `form[formRoot]`, `submit()`) in T13 and T14 | New Reactive Forms or template-driven forms |
| Routing | Lazy `loadComponent` / `loadChildren`, functional guards, route params as signal `input()`s (`withComponentInputBinding()`) | Class guards, `ActivatedRoute` subscriptions |

## TDD workflow (mandatory for T12, T13 and T14)
The exercise requires Test-Driven Development, and the panel evaluates it. The **Steps** below say *what* to build. They are implemented **through** the tests in the **Tests** section, never before them.

1. **Red:** write the spec (`*.spec.ts`) for one unit of behavior first: one service method, interceptor rule, guard or component behavior. Run `npm test`. It must **fail for the right reason**: an assertion failure, or a compile error because the symbol does not exist yet. A test that passes on its first run proves nothing, so fix the test.
2. **Green:** write the minimum code that makes it pass, then run `npm test` again.
3. **Refactor:** clean up with the tests green, then move to the next behavior.
4. **Order of work:** models and pure helpers (`toFieldErrors`, schemas) → services (`AuthService`, `ToastService`) → interceptors and guards → components. Each layer is driven by its own specs, with its dependencies mocked.
5. **Evidence:** in each task's *Implementation notes*, record the red runs, for example "AuthService: 9 specs written first, 9 failed (compile) → green". Commit tests together with their implementation, never implementation without tests.
6. **What not to unit test:** framework wiring (`app.config.ts` provider lists), plain templates with no logic, and styles. These are covered by the build, lint and a manual smoke test.

Test tooling:
- Vitest through `@angular/build:unit-test` (already configured), with jsdom.
- Add `@vitest/coverage-v8` and a `test:coverage` script (`ng test --coverage --watch=false`). Thresholds in the test configuration: **at least 80% lines and branches** for `src/app/core` and `src/app/features`. The build fails below that.
- `HttpTestingController` for HTTP. `HttpTestingController.verify()` in `afterEach`, so no unexpected request goes unnoticed.
- Vitest fake timers for debounce, auto-dismiss and the `refresh_race` retry delay. No real waiting in tests.
- Test names describe behavior (`'retries the original request once with the new token'`), not methods.

## Steps
- [ ] **Project setup:**
  - Confirm zoneless: `zone.js` stays out of `package.json` and `polyfills`. Keep `provideBrowserGlobalErrorListeners()`.
  - Enable strict templates and strict TypeScript (`strict`, `strictTemplates`, `noPropertyAccessFromIndexSignature`).
  - Add ESLint (`ng add angular-eslint`) with the rules `prefer-signals`, `prefer-on-push-component-change-detection`, `no-input-rename` and `prefer-control-flow`, so the conventions are enforced, not only written down.
- [ ] **Folder structure** (standalone, feature-based):
  ```
  src/app/
  ├── core/
  │   ├── auth/      # AuthService, interceptors, guards, session init, auth models
  │   ├── api/       # API base URL, ProblemDetails model, problem → field-errors helper
  │   ├── ui/        # ToastService (signals), ViewportService (signals)
  │   └── error/     # errorInterceptor
  ├── shared/        # reusable components (toast outlet, confirm dialog, form-field errors, spinner), pipes
  ├── features/
  │   ├── auth/      # login, register (T13)
  │   └── heroes/    # list, detail, form (T14)
  └── layout/        # shell, navbar
  ```
- [ ] **`app.config.ts`:**
  - `provideHttpClient(withFetch(), withInterceptors([authInterceptor, refreshInterceptor, errorInterceptor]))`.
  - `provideRouter(routes, withComponentInputBinding(), withViewTransitions())`, with lazy-loaded feature routes.
  - `provideAppInitializer(() => inject(AuthService).restoreSession())`.
- [ ] **`AuthService`** (signal state, in memory):
  - Private `#accessToken = signal<string | null>(null)` and `#user = signal<User | null>(null)`.
  - Public read-only signals: `currentUser = this.#user.asReadonly()`, `isAuthenticated = computed(...)` and `isAdmin = computed(...)`.
  - `login(userName, password)`: `POST /api/auth/login`, then set both signals. The browser stores the refresh cookie by itself.
  - `register(...)`: `POST /api/auth/register`, then `login` (used by T13).
  - `refresh()`: `POST /api/auth/refresh`, **shared while in flight** (`shareReplay(1)` + `finalize`), so parallel 401s trigger **one** refresh. On `401 auth.refresh_race` (T09.1) it retries once after about 200 ms.
  - `logout()`: `POST /api/auth/logout` (revokes the token and deletes the cookie), `clearSession()`, then navigate to `/login`.
  - `clearSession()`: resets both signals without calling the API.
  - `restoreSession()`: one `refresh()` at startup. A failure means "signed out", never an error. Angular waits for it before routing, so guards see the restored session.
  - Multi-tab logout: a `BroadcastChannel('hero-registry-auth')`, opened in the service and closed with `DestroyRef`. `logout()` posts `"logout"`, and other tabs call `clearSession()` and go to `/login`. No token is ever sent on the channel.
- [ ] **Splash on start/F5:** a lightweight CSS spinner inside `<app-root>` in `index.html`, replaced when Angular renders.
- [ ] **`authInterceptor`** (functional): adds `Authorization: Bearer <token>` only to `/api` requests, only when a token exists, and never to `/api/auth/login`, `register`, `refresh` or `logout`. It reads the signal directly (`auth.accessToken()`).
- [ ] **`refreshInterceptor`** (functional):
  - On a **401** from a non-auth `/api` request, call `auth.refresh()` and **retry the original request once** with the new token.
  - Retried requests carry an `HttpContextToken` (`RETRIED`), so they are never retried twice.
  - If the refresh fails: `clearSession()` and navigate to `/login?returnUrl=<current url>`, with the toast "Your session has expired. Please sign in again."
- [ ] **`errorInterceptor`** (functional):
  - **403** shows "You don't have permission to do that".
  - **0 / 5xx** shows a toast with the ProblemDetails `title` (T11). It never shows raw server text.
  - **400 / 409** pass through to the forms (T13, T14).
  - 401 is owned by `refreshInterceptor`.
- [ ] **API helpers (`core/api`):**
  - `ProblemDetails` and `ValidationProblemDetails` types that match the API (T11): `title`, `status`, `code`, `traceId` and `errors`.
  - `toFieldErrors(problem)`: maps the camelCase `errors` dictionary to form fields. T13 and T14 use it with Signal Forms to show server errors next to the right field.
  - The API base URL `/api` is proxied in development (Aspire) and served by nginx in Docker. No `withCredentials` and no CORS credentials, because everything is same-origin, so the browser sends the `SameSite=Strict` cookie automatically.
- [ ] **Data-fetching pattern** (reference implementation for T14):
  - Document in `core/api/README.md`: reads use `httpResource(() => ({ url, params }))` with signal parameters, so changing `search()` or `page()` refetches automatically and cancels the previous request. Writes use `HttpClient` followed by `resource.reload()`.
  - Example: `currentUser` details from `GET /api/auth/me` with `httpResource`, shown in the navbar's profile menu.
- [ ] **`ToastService`** (signals): a `toasts` signal of `{ id, kind, text }`, with `show()` and `dismiss()` and auto-dismiss after about 5 s. A `<app-toast-outlet>` in the shell renders it with `@for` and `aria-live="polite"`.
- [ ] **`ViewportService`** (signals): an `isMobile` signal from `matchMedia('(max-width: 768px)')`, with the listener cleaned up by `DestroyRef`. This avoids adding `@angular/cdk` only for breakpoints.
- [ ] **Guards** (functional `CanActivateFn`): `authGuard` (reads `isAuthenticated()`) and `adminGuard` (reads `isAdmin()`). Both return a `UrlTree` to `/login?returnUrl=...`, or to `/heroes` for a non-admin.
- [ ] **Responsive layout:**
  - `ShellComponent` and `NavbarComponent` use `input()` / `output()` and OnPush (the default).
  - The navbar shows the user name, an admin badge (`@if (auth.isAdmin())`) and logout. A collapsible menu is driven by `isMobile()` and a local `menuOpen = signal(false)`.
  - Mobile-first SCSS with CSS custom properties (light and dark), or Angular Material. Choose one and stay consistent.
  - `document.title` is updated through the router's `title` route property (no effect needed).

## Tests (TDD, Vitest and Angular TestBed, zoneless)
Zoneless testing rules:
- No `fakeAsync` or `zone.js` testing helpers.
- Use `await fixture.whenStable()` or `TestBed.tick()` after changing signals, and read signal values directly in assertions.
- For HTTP, use `provideHttpClient()` + `provideHttpClientTesting()` + `HttpTestingController`, then `TestBed.tick()` so `httpResource` issues its request.

- [ ] **`AuthService`:**
  - Login stores the token in memory and updates `currentUser`, `isAuthenticated` and `isAdmin`.
  - **Nothing is written to `localStorage` or `sessionStorage`** (spy on both).
  - Logout calls the API and clears the signals.
  - `restoreSession()`: a successful refresh restores the user, and a 401 leaves the app signed out without throwing.
  - Parallel `refresh()` calls produce **one** HTTP request.
  - `auth.refresh_race` is retried once.
- [ ] **`authInterceptor`:** adds the header only when a token exists and only for `/api`, and never for the `/api/auth/*` token endpoints.
- [ ] **`refreshInterceptor`:**
  - 401, then refresh, then the retried request carries the new token and succeeds.
  - Two parallel 401s cause one refresh and two retries.
  - A failed refresh calls `clearSession()` and navigates to `/login?returnUrl=...`.
  - A retried request that gets 401 again is not retried a second time.
- [ ] **`errorInterceptor`:** 403 and 5xx push a toast, and 400 and 409 pass through.
- [ ] **`toFieldErrors`:** maps `errors.heroName` to the `heroName` field, ignores unknown keys, and handles a ProblemDetails with no `errors`.
- [ ] **`ToastService`:** `show` adds a toast, auto-dismiss removes it (Vitest fake timers), and `dismiss` removes it.
- [ ] **`ViewportService`:** `isMobile` follows a mocked `matchMedia` change.
- [ ] **Multi-tab:** a `"logout"` message on the channel clears the session.
- [ ] **Guards:** allow or redirect correctly, including `returnUrl`.
- [ ] **`NavbarComponent`:** the admin badge appears only when `isAdmin()` is true, and the logout button calls `AuthService.logout()`.

## Done when
- The app runs zoneless (no `zone.js` in the bundle), and ESLint passes with the signal and OnPush rules.
- A signed-in user stays signed in after **F5** and after the 15-minute access token expires, with no visible interruption.
- DevTools → Application shows **no token in Local Storage or Session Storage**, and the `hr_refresh` cookie is `HttpOnly`.
- Logging out in one tab logs out the others.
- `npm test`, `npm run lint` and `npm run build` pass with no warnings, and the browser console is clean.
- **TDD evidence:** every spec listed in Tests exists and was red before its implementation (recorded in the Implementation notes). `npm run test:coverage` meets the 80% line and branch thresholds for `core`.

## Presentation notes
- **Signals:** "The app is zoneless and OnPush. Every piece of UI state is a signal or a computed value, reads use `httpResource`, forms use Signal Forms, and RxJS is kept for streams such as the token refresh in the interceptors."
- **Tokens:** "The access token is kept in memory to reduce XSS exposure. The refresh token is an HttpOnly, SameSite=Strict cookie with rotation and reuse detection. One interceptor attaches the token, and another silently refreshes on 401, sharing a single in-flight refresh and retrying once. The user only sees the login page when the refresh token itself is no longer valid."
