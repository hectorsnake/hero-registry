# Angular guidelines (generic)

Condensed best practices for modern Angular (v20+). When the project's own conventions (CLAUDE.md, task
docs, lint and formatter configuration) disagree with this file, **the project wins**.

## Principles
1. Minimize bad dependencies and maximize good ones.
2. Locality of behavior: keep related code together so it is clear and easy to refactor.
3. Prefer code duplication over bad abstractions.
4. Prefer composition over inheritance.
5. Use modern Angular APIs (zoneless, SSR-friendly) instead of the old ones.
6. Take a signal-based approach, so there are fewer subscriptions to manage.
7. Mix paradigms deliberately: classes for components and services, pure functions for mapping, and
   interfaces or abstract classes to cut bad dependencies.

## Formatting and naming
- **Indentation, quotes, line width:** whatever `.editorconfig` / `.prettierrc` say. Run the formatter.
- **Identifiers and methods:** camelCase.
- **Classes, enums, enum values and as-const objects:** PascalCase.
- **Module-level constants:** UPPER_SNAKE_CASE.
- **Interfaces:** no `I` prefix (`Product`, not `IProduct`).
- **Files:**
  - Hyphen-separated, and the file name matches the identifier inside (`product-list.ts` holds `ProductList`).
  - A component's `.ts`, `.html`, `.scss` and `.spec.ts` share one name and sit in one folder.
  - Angular 20+ style-guide names: no `.component` / `.service` suffixes (unless the project still uses them).

## Folder structure
- Organize by feature: `src/app/products/product-list/`, `src/app/products/product-form/`, ...
- Don't create folders by type (`components/`, `services/`, `directives/`).
- Split a folder further when it gets crowded.
- An `index.ts` at a feature edge exports only the public API. No barrel re-exports of everything.

## Components, directives, services
**Naming**
- **Component selectors:** kebab-case with the project prefix (`app-product-list`).
- **Directive selectors:** camelCase, prefixed, and they must not collide with DOM attributes.
- **Inputs:** no prefix, no collision with DOM attributes.
- **Event handlers:** named for what they do (`save()`, not `onClick()`). One- to three-line handlers stay inline.
- **Outputs:** no `on` prefix, prefer the past tense (`saved`, `deleted`).

**Class layout**
1. Injected services.
2. Inputs, outputs, models and queries.
3. Computed values and other signals.
4. Methods (public, then private).
- Move methods that don't use `this` out of the class as plain functions.
- Declare implemented interfaces explicitly.

**Modifiers**
- **`readonly`:** on everything Angular initializes (`inject`, `input`, `output`, `viewChild`, signals).
- **`#private`:** for anything that isn't template or public API.
- **`protected`:** for members used only by the template.
- **No `public` keyword:** it is the default.
- Tests exercise behavior, not private members.

**Modern Angular**
- Standalone components with OnPush (the default in recent versions); import only what the template uses, no `CommonModule`.
- `[class.x]` / `[style.x]` over `ngClass` / `ngStyle`; `host: {}` over `@HostBinding` / `@HostListener`.
- `signal` instead of `BehaviorSubject`, unless you really need event semantics.
- Signal APIs (`input()`, `output()`, `model()`, `viewChild()`, `viewChildren()`, `contentChild()`,
  `contentChildren()`) instead of their decorators.
- `afterNextRender` / `afterEveryRender` instead of `ngAfterViewInit`; `DestroyRef` instead of `ngOnDestroy`.
- Built-in `resource()` / `httpResource()` / `rxResource()` for async data; no extra library needed.
- **Exception:** RxJS remains the right tool for an existing Observable API, or for an ordered event stream
  with complex processing (interceptors, retries, debounce).

**Styles scope**
- Avoid `ViewEncapsulation.None` and `::ng-deep`. If unavoidable, scope it: `:host ::ng-deep ...`.
- Prefer `:host-context` when you own both components.

## Services
- **API services:** inject only `HttpClient` (plus a base-URL token if the project has one), map endpoints to
  methods, `providedIn: 'root'`, no state.
- **Stateful services:**
  - Provide them at component level (`providers: [...]` on the feature component), except for global concepts
    (auth, toasts) that guards and interceptors need. Root-provided state must clean itself up.
  - Structure: private state signals (`readonly #items = signal<...>(...)`), read-only exposure
    (`readonly items = this.#items.asReadonly()`), selectors (`readonly count = computed(...)`), and actions
    (methods that update the state).
  - Consider NgRx SignalStore when the state gets complex.

## Extra rules
- **No getters or setters.** Use `computed()` or explicit methods.
- **`undefined` vs `null`:** `undefined` for optional properties and inputs; `null` for serializable data and
  types that already include null (API JSON usually uses `null`).
- **Types:** always type parameters; `unknown`, never `any`; return types on public and exported functions;
  don't annotate inferable member types (signals, inputs, queries).
- **Templates:** never call methods that return new arrays, objects or Observables, or heavy functions;
  precompute view data in `computed()`.
- **Unsubscribing:** `takeUntilDestroyed()` when a subscription is unavoidable.
- **Side effects (`effect`, `subscribe`):**
  - Keep them to a minimum. Connect the reactive graph directly with `toSignal`, `computed`, `linkedSignal`,
    `resource` / `httpResource`.
  - Never `effect` or `subscribe` just to copy a value into another signal.
  - Effects are fine for logging, syncing with storage, custom DOM work and third-party rendering.
- **Loading data:** expose loading / loaded / error states; with resources these come from `isLoading()`,
  `value()`, `error()` and `hasValue()`.
- **Comments:** only for complex algorithms, TODO / FIXME, attributions, context for AI, JSDoc on public API
  and linter directives. No comment is better than a bad one.

## CSS / SCSS
- **BEM naming:** `.block`, `.block__element`. Modifiers in data attributes, not `--modifier` classes:
  `<div class="card" data-variant="featured">` → `.card[data-variant="featured"]`.
  Don't mirror DOM depth: `.card__title`, never `.card__body__title`.
- **Utility classes:** sparingly, for layout tweaks at the usage site; may use `!important`; never style a
  component's whole look with utilities, never inline `style=""`.
- **Layout and position:** a component never sets its own position, outer margin or context-dependent size;
  the parent or utilities do.
- **No style leaks:** don't target child component internals or use broad element selectors.
- **Selectors:** no ids or bare elements (ids are for tests and accessibility); no `!important` outside
  utilities; at most 2 chained classes; keep specificity low.
- **Values:** rem/em, not px (px is fine for borders and outlines); logical properties (`margin-inline`,
  `padding-block`, `inset-inline-start`, `inline-size`, `block-size`); never hard-code colors: use the
  project's design tokens (UI-library system tokens or app custom properties) for color, typography, shape
  and spacing.
- **Custom properties:** scope component variables to the block, not `:root`.
- **Nesting:** properties before nested rules; order: own properties → pseudo-classes → pseudo-elements →
  `&__element` → media/container queries; maximum 3 levels.
- **Responsive:** nest queries inside the selector they change; prefer `@container` or wrapping layouts;
  keep `@media` for real viewport concerns. Design mobile-first.
- **Prefer existing components** from the project's UI library before writing custom CSS.

## References
- Angular Style Guide: https://angular.dev/style-guide
- NG Tips: https://ngtips.com/
- Google TypeScript Style Guide: https://google.github.io/styleguide/tsguide.html
- Airbnb JavaScript Style Guide: https://github.com/airbnb/javascript
