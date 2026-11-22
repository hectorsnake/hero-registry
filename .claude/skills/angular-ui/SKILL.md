---
name: angular-ui
description: Build or change anything in an Angular (v20+) front end - features, pages, components, forms, tables, dialogs, API services, state services, routes, guards, interceptors and styles - with modern, signals-first Angular (standalone, zoneless, OnPush, signal inputs, httpResource, Signal Forms when available). Discovers the project's conventions (test runner, forms, UI library, API base URL, formatting) from the repository before writing code. Use when the user asks for a screen, component or service in the Angular UI, to consume a backend endpoint from Angular, or to fix or refactor Angular code. Every unit of logic it creates gets a spec via the angular-unit-test skill, test-first when the project requires TDD.
---

# Angular UI (generic, signals-first)

## 1. Discover the project first (never assume)
Before writing code, read these and note the answers. Where the project's own docs (CLAUDE.md, task files,
README, ADRs) set a convention, **the project wins over this skill**.

| Question | Where to look |
| --- | --- |
| Angular version, test runner | `package.json` (`@angular/core`; `vitest` vs `karma`/`jasmine-core`), `angular.json` → `architect.test.builder` (`@angular/build:unit-test` = Vitest, `@angular/build:karma` = Karma) |
| Zoneless? | `zone.js` absent from `package.json` and `polyfills` → zoneless (the default from Angular 21) |
| Formatting | `.editorconfig`, `.prettierrc` (indent size, quotes, print width). Run the project's formatter if it has one |
| Selector prefix, style language | `angular.json` → `prefix`, `schematics` |
| UI library | `package.json` (`@angular/material`, PrimeNG, none). If none was chosen, ask before adding one |
| API base URL | An injection token (`API_BASE_URL`), `environment*.ts`, or a relative `/api` behind a dev proxy (`proxy.conf.*`). Never hard-code hosts in features |
| Forms | `@angular/forms/signals` available (Angular 21+) and chosen by the project → **Signal Forms**; otherwise typed Reactive Forms (`NonNullableFormBuilder`) |
| Lint rules | `eslint.config.*` (angular-eslint rules such as prefer-signals, OnPush, control flow) |
| E2E test-id convention | Docs or an existing `src/testing/test-ids.ts` helper. Apply section 5 only if the project uses one |
| TDD required? | Task docs / CLAUDE.md. If yes, follow the red → green → refactor loop in the angular-unit-test skill |

Then read the **API contract** for the endpoints you consume (OpenAPI document, controller or endpoint
files, DTOs): routes, verbs, query parameters, status codes, error shape (usually RFC 9457 ProblemDetails,
with `errors` for validation), JSON casing, enum and date formats.

## 2. Workflow for a feature
1. **Generate** with the CLI so files follow the project's naming (Angular 20+ style guide: no
   `.component` suffix): `npx ng g c <feature>/<feature>-list`, `npx ng g s <feature>/<feature>-api`.
   Use feature folders (`src/app/<feature>/...`), never `components/` or `services/` type folders. Shared
   cross-feature code goes in `shared/` (or the project's equivalent, such as `core/`).
2. **Models:** interfaces mirroring the API DTOs, no `I` prefix. Enum strings as string-literal unions or
   as-const objects. Nullable API fields are `T | null`. Generic shapes (`PagedResult<T>`, `ProblemDetails`,
   `ValidationProblemDetails`) live in shared code.
3. **Specs first when TDD is required** (angular-unit-test skill), then the code that makes them pass.
4. **API service:** HTTP only, no state (pattern below).
5. **Components:** signals, `input()`/`output()`/`model()`, `httpResource`/`rxResource` for loading,
   `computed()`/`linkedSignal()` for derived view data. No `effect`/`subscribe` that copies values into signals.
6. **Route:** lazy, `loadComponent: () => import('./<feature>/<feature>-list/<feature>-list').then((m) => m.FeatureList)`.
   Route params and query params arrive as signal inputs when `withComponentInputBinding()` is enabled.
7. **Verify:** formatter, lint, `build` (no errors or warnings) and the full unit test run. For UI work, also
   serve the app and check the page against the running API, with a clean browser console.

## 3. Rules most often missed
Read `references/angular-guidelines.md` for the full list. The essentials:
- **Zoneless + OnPush:** never add `zone.js`; no `ChangeDetectorRef.detectChanges()` workarounds.
- **Signal APIs over decorators:** `input()`, `input.required()`, `output()`, `model()`, `viewChild()`,
  `contentChild()`; `inject()` over constructor injection; `host: {}` over `@HostBinding`/`@HostListener`.
- **Templates:** `@if`/`@for` (always with `track`)/`@switch`/`@defer`; `[class.x]`/`[style.x]` over
  `ngClass`/`ngStyle`; never call methods that build new arrays or objects; precompute in `computed()`.
- **RxJS only where streams fit:** interceptors, retry/refresh flows, debounce. Bridge with `toSignal()` /
  `toObservable()`. No `.subscribe()` in components; if unavoidable, `takeUntilDestroyed()`.
- **State:** private writable signals, public `asReadonly()`, `computed` selectors, action methods.

## 4. Patterns

**API service**
```ts
@Injectable({ providedIn: 'root' })
export class ProductApi {
  readonly #http = inject(HttpClient);
  readonly #url = '/api/products'; // or `${inject(API_BASE_URL)}/api/products`, per the project

  create(request: ProductRequest): Observable<Product> {
    return this.#http.post<Product>(this.#url, request);
  }
}

// Reads as a resource factory, called from an injection context (component field initializer):
export function productsResource(query: Signal<ProductQuery>) {
  return httpResource<PagedResult<Product>>(() => ({ url: '/api/products', params: toParams(query()) }));
}

// Outside the class: it doesn't use `this`.
function toParams(query: ProductQuery): Record<string, string | number> { /* skip empty values */ }
```

**Loading data in a component** (re-runs when `query()` changes, cancels the previous request):
```ts
readonly page = input(1, { transform: numberAttribute }); // from the query string
protected readonly query = computed(() => ({ page: this.page(), pageSize: 20 }));
protected readonly products = productsResource(this.query);
protected readonly rows = computed(() => this.products.value()?.items ?? []);
```
Template: `@if (products.isLoading())` → skeleton; `@else if (products.error())` → message + retry
(`products.reload()`); `@else if (!rows().length)` → empty state; `@else` → `@for (p of rows(); track p.id)`.
After a write, call `reload()` on the affected resource.

**Forms: Signal Forms** (`@angular/forms/signals`, when the project uses them)
```ts
protected readonly model = signal<ProductForm>({ name: '', price: 0 });
protected readonly productForm = form(this.model, (p) => {
  required(p.name);
  maxLength(p.name, 100);
  min(p.price, 0);
});

protected async save(): Promise<void> {
  await submit(this.productForm, async () => {
    try {
      await firstValueFrom(this.#api.create(this.model()));
      return undefined;
    } catch (error) {
      return toFieldErrors(error); // map ProblemDetails `errors` onto the fields
    }
  });
}
```
```html
<form [formRoot]="productForm" (submit)="save()">
  <input [formField]="productForm.name" />
  @if (productForm.name().touched() && productForm.name().invalid()) { ... }
</form>
```
- Edit mode: `model = linkedSignal(() => toFormModel(this.existing.value()) ?? empty())`, not `patchValue`.
- Cross-field rules with `validateTree`; per-item rules with `applyEach`; reusable rules with `schema()` + `apply`.
- **Fallback (no Signal Forms):** typed Reactive Forms via `inject(NonNullableFormBuilder)`; map server
  errors with `control.setErrors({ server: message })`.
- Either way: disable submit while pending; 400/409 field errors go next to the field (camelCase keys, possibly
  paths like `items[0].name`); other problems show the ProblemDetails `title`/`detail` in a toast or message area.

**Interceptors and guards:** functional (`HttpInterceptorFn`, `CanActivateFn`), registered with
`provideHttpClient(withInterceptors([...]))`. Guards return `true` or a `UrlTree`.

**Auth tokens (browser best practice):** access token in memory (a signal), refresh token in an HttpOnly
cookie managed by the API; never `localStorage`/`sessionStorage` for tokens unless the project explicitly
accepts that trade-off.

## 5. Test ids (only if the project uses an e2e test-id convention)
- Format: `{component}-{name}-{kind}` in kebab-case; kinds such as `input`, `select`, `label`, `error`,
  `button`, `link`, `table`, `row`, `option`, `dialog`.
- Form controls and their labels: `id` and `data-testid` set to the same value.
- Repeated elements: bind with a stable key, never the index: `[attr.data-testid]="'product-list-row-' + p.id"`.
- Reusable components rendered more than once: a `testIdPrefix = input('<component>')` used in a `computed()`.
- Overlay content (options, menus, dialogs, snackbars) gets ids too.
- Never style by id or `data-testid`. Treat the ids as a public contract; don't rename them casually.

## 6. Styles
- Follow the project's design tokens (CSS custom properties from the UI library or `styles.scss`); never
  hard-code colors. BEM classes, data attributes for variants, rem and logical properties.
- No `::ng-deep`, no id or bare element selectors, nesting at most 3 levels, mobile-first, prefer
  `@container` or wrapping layouts over `@media` when the size depends on the container.
- Stay under the project's component style budget (`angular.json` → `budgets`).

## 7. Integration notes
- **Same-origin proxy vs CORS:** prefer a dev proxy (`proxy.conf.*`) and a reverse proxy in production so the
  UI and API share an origin (no CORS, cookies just work). If the project calls the API cross-origin, the
  API must allow the UI origin; confirm with the user before changing backend code.
- **HTTPS dev certificates:** if the API uses a local development certificate, the browser must trust it.
