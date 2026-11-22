# T14: Angular hero management (list, detail, form and delete)

**User story:** [US-001](../US-001-SuperHero-Registry.md) (AC4 to AC9) · **Layer:** UI · **Estimate:** 10 h · **Depends on:** T10, T12

## Goal
Provide a responsive, user-friendly UI for all hero CRUD operations. It is built **signals-first** following the [T12](T12-Angular-Core.md) conventions:
- **`httpResource()`** for every read.
- **`HttpClient`** for writes, followed by `reload()`.
- **Signal Forms** for the hero form.
- Signal `input()`s for route parameters.
- Zoneless and OnPush, with no `.subscribe()` in components.

## Steps
- [ ] **Models (`features/heroes/hero.models.ts`):** `Hero`, `HeroRequest`, `HeroQuery` (`search`, `page`, `pageSize`, `sortBy`, `sortDirection`) and `PagedResult<T>`, matching the API (T10). `birthDate` is a `yyyy-MM-dd` string, and `superpowers` is `string[]`.
- [ ] **`HeroApi` (`features/heroes/hero.api.ts`)**:
  - **Read factories that return resources**, called from an injection context:
    - `heroesResource(query: Signal<HeroQuery>)` = `httpResource<PagedResult<Hero>>(() => ({ url: '/api/heroes', params: toParams(query()) }))`.
    - `heroResource(id: Signal<number | undefined>)` returns `undefined` while there is no id, so no request is made.
    - Changing a signal parameter refetches automatically and cancels the previous request.
  - **Writes** return `Observable`s or `Promise`s: `create(request)`, `update(id, request)` and `delete(id)`, using `HttpClient`.
- [ ] **`HeroListComponent` (`/heroes`)**, with the URL as the source of truth for the query:
  - Route query parameters as signal inputs (`search = input<string>()`, `page = input(1, { transform: numberAttribute })`, `sortBy`, `sortDirection`) via `withComponentInputBinding()`.
  - `query = computed(() => ({ ... }))` feeds `heroesResource(query)`.
  - Changing the search, sort or page calls `router.navigate([], { queryParams, queryParamsHandling: 'merge' })`, so the list is shareable and the back button works.
  - **Search box:** a local `searchText = linkedSignal(() => this.search() ?? '')`, debounced at 300 ms with `toObservable` + `debounceTime` + `toSignal`, before writing the URL. A new search resets `page` to 1.
  - **States** from the resource signals:
    - `@if (heroes.isLoading())` shows skeleton rows.
    - `@else if (heroes.error())` shows an error panel with a **Retry** button (`heroes.reload()`).
    - `@else if (heroes.value()?.items.length === 0)` shows an empty state with a "Register a hero" call to action.
    - `@else`, the list.
  - **Layout:** a table on desktop and cards on mobile, switched by `viewport.isMobile()` (T12). `@for (hero of items(); track hero.id)`.
  - Superpowers are shown as chips, and the power level as a bar with `aria-valuenow`.
  - **Paginator component** (`shared`): `page = model.required<number>()`, `totalPages = input.required<number>()` and `pageSize = input<number>()`. Keyboard accessible.
- [ ] **`HeroDetailComponent` (`/heroes/:id`):**
  - `id = input.required<number, string>({ transform: numberAttribute })` and `hero = heroResource(this.id)`.
  - Read-only view with audit info (created and updated by and at, with relative time through a pipe).
  - Edit button, and a Delete button `@if (auth.isAdmin())`.
  - A 404 (`hero.not_found`) shows a "Hero not found" panel with a link back to the list.
- [ ] **`HeroFormComponent` (`/heroes/new` and `/heroes/:id/edit`), with Signal Forms:**
  - `id = input<number | undefined, string | undefined>(undefined, { transform: ... })`, `isEdit = computed(() => this.id() !== undefined)` and `existing = heroResource(this.id)`.
  - **Model:** `model = linkedSignal(() => toFormModel(this.existing.value()) ?? emptyHero())`. In edit mode it fills in when the hero loads. In create mode it starts empty. No `patchValue` and no effect.
  - `heroForm = form(this.model, heroSchema)`, bound with `<form [formRoot]="heroForm">` and `[formField]` on every input.
  - **`heroSchema` (`hero.schema.ts`)** mirrors the server (US-001, SuperHero table):
    - `heroName`: `required`, `minLength(2)`, `maxLength(100)`.
    - `firstName` and `lastName`: `required`, `maxLength(100)`.
    - `birthDate`: `required`, `maxDate(yesterday)`, plus a `validate` rule for "no more than 150 years ago".
    - `email`: `required`, `email`, `maxLength(256)`.
    - `superpowers`: a `validate` rule for 1 to 10 items with no case-insensitive duplicates, plus `applyEach` for 2 to 50 characters per item.
    - `powerLevel`: `required`, `min(1)`, `max(100)`.
    - `address1`: `required`, `maxLength(200)`. `address2`: `maxLength(200)`. `city`: `required`, `maxLength(100)`.
    - `state`: `required`, plus a `validate` rule that it is one of the 50 US codes (plus DC), shared with the select options.
    - `zipCode`: `required`, `pattern(/^\d{5}(-\d{4})?$/)`.
  - **Superpowers chip input** (`shared/chips-input`): `value = model.required<string[]>()`. Enter or comma adds a chip (trimmed), Backspace on an empty input removes the last one, and the control is disabled at 10 items.
  - **BirthDate:** a native `<input type="date" [max]="today">`, with no extra date library.
  - **PowerLevel:** a range slider plus a number input bound to the same field.
  - **State:** a `<select>` of US states. **ZipCode:** `inputmode="numeric"`.
  - **Submit** through `submit(heroForm, ...)`:
    - `create` or `update` (bridged with `firstValueFrom`).
    - **400 / 409** are mapped with `toFieldErrors(problem)` and returned from the action, so they attach to the fields (for example a 409 with `errors.heroName` shows "This hero name is already registered").
    - On success: a toast ("Hero saved"), then navigate to the detail page.
    - `pending` signal for the submit button.
  - **Unsaved-changes guard:** a functional `canDeactivate` that asks for confirmation when `heroForm().dirty()` and the form wasn't just saved (a `saved = signal(false)`).
- [ ] **Delete** (admins only):
  - The button is visible only when `auth.isAdmin()`. It opens the shared confirm dialog (`<dialog>`-based, with `open = model<boolean>()`), naming the hero.
  - On confirm: `heroApi.delete(id)`, then a toast ("Hero deleted"), then `heroes.reload()` on the list, or navigate to the list from the detail page.
  - A 403 is handled by T12's `errorInterceptor` toast. A 404 means it was already deleted, so reload.
- [ ] **Performance:**
  - The form route and the chips input are lazy-loaded (`loadComponent`).
  - `@defer (on viewport)` for heavy, non-critical blocks (for example the audit timeline).
  - `track hero.id` in every `@for`.
- [ ] **Accessibility:**
  - Table headers use `scope` and `aria-sort` on the sorted column.
  - Errors use `aria-describedby` (`FormFieldErrorComponent` from T13).
  - Focus returns to the list after a delete.

## Tests (TDD, Vitest, zoneless TestBed, `HttpTestingController`)
Call `TestBed.tick()` so a resource issues its request, then flush it and `await fixture.whenStable()`.

- [ ] **`HeroApi`:**
  - `heroesResource` builds the right URL and query parameters from the query signal, and **refetches when the signal changes**.
  - `heroResource(undefined)` makes no request.
  - The writes use the correct verbs, URLs and bodies.
- [ ] **`heroSchema`:**
  - Every rule's boundaries: name lengths 1/2/100/101, power level 0/1/100/101, a future birth date, more than 150 years ago, an invalid ZIP or state.
  - Superpowers: 0 and 11 items, duplicates ignoring case, and item length.
- [ ] **`HeroListComponent`:**
  - The query parameter inputs drive the request.
  - Search is debounced (Vitest fake timers) and resets the page to 1.
  - The loading, error with retry, and empty states render.
  - The delete action is hidden for non-admins, and confirming calls delete and reloads.
- [ ] **`HeroDetailComponent`:** loads by the `id` input, and a 404 shows the not-found panel.
- [ ] **`HeroFormComponent`:**
  - Create mode starts empty. Edit mode fills from the resource through `linkedSignal`.
  - Submit sends the correct DTO (trimmed values, `superpowers` array).
  - A server 409 maps to `heroName`.
  - The `canDeactivate` guard asks when the form is dirty and not after a save.
- [ ] **Chips input:** add, remove, trim, blocks duplicates, and the 10-item limit.

## Done when
- Every hero CRUD operation works end to end against the API, and the URL reflects search, sort and page.
- The layout is usable at 360 px and on desktop, and there are no console warnings.
- No manual `.subscribe()` in components, and no Reactive or template-driven forms. ESLint (T12 rules) passes.
