# T12: Angular core (auth service, interceptor, guards and layout)

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** UI (`hero-registry-ui`) · **Estimate:** 5 h · **Depends on:** T09

## Goal
Create the base structure of the Angular app: a clean folder layout, authentication state, HTTP plumbing and a responsive shell.

## Steps
- [ ] Folder structure (standalone components, feature-based):
  ```
  src/app/
  ├── core/        # auth (service, interceptor, guards), api config, error handling, models
  ├── shared/      # reusable UI components (confirm dialog, form-field errors, spinner), pipes
  ├── features/
  │   ├── auth/    # login, register (T13)
  │   └── heroes/  # list, detail, form (T14)
  └── layout/      # shell, navbar
  ```
- [ ] `provideHttpClient(withInterceptors([...]))` and `provideRouter(routes, withComponentInputBinding())`, with lazy-loaded feature routes.
- [ ] `AuthService` (signal-based state):
  - Signals: `currentUser`, `isAuthenticated`, `isAdmin`
  - Methods: `login`, `register`, `logout`
  - Stores the token in `sessionStorage` and decodes expiration (logs out automatically when the token expires).
- [ ] `authInterceptor`: adds the `Authorization: Bearer` header to `/api` requests.
- [ ] `errorInterceptor`: a 401 response logs out and redirects to `/login?returnUrl=...`. Other errors produce a toast or snackbar message.
- [ ] Guards: `authGuard` (authenticated) and `adminGuard`.
- [ ] Environment and API base URL: `/api`, which is proxied in development (Aspire) and served by nginx in Docker.
- [ ] Responsive layout: a navbar with the user name, an admin badge and logout. Mobile-first SCSS with CSS variables, or Angular Material. Choose one and stay consistent.

## Tests (TDD, Vitest and Angular TestBed)
- [ ] `AuthService`: login stores the token and updates signals. Logout clears them. An expired token counts as unauthenticated.
- [ ] `authInterceptor` adds the header only when a token exists. `errorInterceptor` handles 401 by redirecting.
- [ ] Guards allow or redirect correctly.

## Done when
- `npm test` and `npm run build` pass with no warnings, and the browser console is clean.
