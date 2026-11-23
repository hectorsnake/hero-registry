# T14: Angular hero management (list, detail, form and delete)

**User story:** [US-001](../US-001-SuperHero-Registry.md) (AC4 to AC9) · **Layer:** UI · **Estimate:** 10 h · **Depends on:** T10, T12 · **Status:** Done

## Goal
Provide a responsive, user-friendly UI for all hero CRUD operations. It is built **signals-first** following the [T12](T12-Angular-Core.md) conventions:
- **`httpResource()`** for every read.
- **`HttpClient`** for writes, followed by `reload()`.
- **Signal Forms** for the hero form.
- Signal `input()`s for route parameters.
- Zoneless and OnPush, with no `.subscribe()` in components.

## TDD (mandatory)
Follow the [T12 TDD workflow](T12-Angular-Core.md#tdd-workflow-mandatory-for-t12-t13-and-t14): red → green → refactor, one behavior at a time, with evidence in the Implementation notes. Suggested order, each driven by its specs from the Tests section:
1. `heroSchema` (pure): every field's boundaries, superpowers and state codes.
2. `HeroApi`: resource URLs and parameters, refetch when the query signal changes, no request for an `undefined` id, write verbs and bodies.
3. Shared components: chips input, paginator, confirm dialog. Then the presentational `HeroTableComponent` and `HeroCardComponent` (columns, sort headers, actions column, admin-only Delete).
4. `HeroListComponent` → `HeroDetailComponent` → `HeroFormComponent` (create, edit, server errors, `canDeactivate`).

## Steps
- [x] **Models (`features/heroes/hero.models.ts`):** `Hero`, `HeroRequest`, `HeroQuery` (`search`, `page`, `pageSize`, `sortBy`, `sortDirection`) and `PagedResult<T>`, matching the API (T10). `birthDate` is a `yyyy-MM-dd` string, and `superpowers` is `string[]`.
- [x] **`HeroApi` (`features/heroes/hero.api.ts`)**:
  - **Read factories that return resources**, called from an injection context:
    - `heroesResource(query: Signal<HeroQuery>)` = `httpResource<PagedResult<Hero>>(() => ({ url: '/api/heroes', params: toParams(query()) }))`.
    - `heroResource(id: Signal<number | undefined>)` returns `undefined` while there is no id, so no request is made.
    - Changing a signal parameter refetches automatically and cancels the previous request.
  - **Writes** return `Observable`s or `Promise`s: `create(request)`, `update(id, request)` and `delete(id)`, using `HttpClient`.
- [x] **`HeroListComponent` (`/heroes`)**, with the URL as the source of truth for the query:
  - Route query parameters as signal inputs (`search = input<string>()`, `page = input(1, { transform: numberAttribute })`, `sortBy`, `sortDirection`) via `withComponentInputBinding()`.
  - `query = computed(() => ({ ... }))` feeds `heroesResource(query)`.
  - Changing the search, sort or page calls `router.navigate([], { queryParams, queryParamsHandling: 'merge' })`, so the list is shareable and the back button works.
  - **Search box:** a local `searchText = linkedSignal(() => this.search() ?? '')`, debounced at 300 ms with `toObservable` + `debounceTime` + `toSignal`, before writing the URL. A new search resets `page` to 1.
  - **Page layout:**
    ```
    ┌──────────────────────────────────────────────────────────────────────────┐
    │ Heroes                                                   [ + Add hero ]  │
    │ [ 🔍 Search heroes...            ]                     12 heroes found   │
    ├───────────────┬──────────────────┬──────────────┬───────┬────────────────┤
    │ Hero name ▲   │ Real name        │ Superpowers  │ Power │ Actions        │
    ├───────────────┼──────────────────┼──────────────┼───────┼────────────────┤
    │ Spider-Man    │ Peter Parker     │ [Agility] +2 │ ███░  │ 👁  ✏️  🗑     │
    │ Superman      │ Clark Kent       │ [Flight] +3  │ ████  │ 👁  ✏️  🗑     │
    ├───────────────┴──────────────────┴──────────────┴───────┴────────────────┤
    │                                          ‹ Prev   Page 1 of 3   Next ›   │
    └──────────────────────────────────────────────────────────────────────────┘
    ```
  - **Toolbar** (above the table):
    - The page title "Heroes".
    - An **"+ Add hero"** primary button, a `routerLink` to `/heroes/new`. It is visible to **every signed-in user** (AC4) and is always shown, not only in the empty state.
    - The search box.
    - A result count from `totalCount`, in an `aria-live="polite"` region.
  - **Table (desktop)**, `@for (hero of items(); track hero.id)`. Columns, in order:

    | Column | Content | Sortable (`sortBy`) |
    | ------ | ------- | ------------------- |
    | Hero name | `heroName` (bold) | ✅ `HeroName` |
    | Real name | `firstName lastName` | No |
    | Superpowers | The first 2 as chips, then a "+N" chip with the full list in its `title` and `aria-label` | No |
    | Power | A bar with `role="meter"`, `aria-valuenow`, `aria-valuemin="1"`, `aria-valuemax="100"`, and the number | ✅ `PowerLevel` |
    | Created | Relative date (`createdAt`), hidden below 1024 px | ✅ `CreatedAt` |
    | **Actions** (last column, right-aligned) | **View**, **Edit** and **Delete** icon buttons | No |

    - **Sorting:** click a sortable header to sort by that column (ascending). Click again to toggle the direction. The header shows ▲ or ▼ and sets `aria-sort`. Sort state lives in the URL (`sortBy`, `sortDirection`).
    - Hero names and the other cells are plain text. Navigation happens only through the action buttons, so a row has one clear click target per action.
  - **Actions column** (per row):
    - **View** (eye icon): a `routerLink` to `/heroes/:id`.
    - **Edit** (pencil icon): a `routerLink` to `/heroes/:id/edit`.
    - **Delete** (trash icon): rendered **only** `@if (auth.isAdmin())` (AC8). It opens the confirm dialog (see Delete). Regular users see only View and Edit, and the column keeps its width so the layout doesn't jump.
    - Each is a real `<a>` or `<button>` with an `aria-label` that includes the hero ("View Superman", "Edit Superman", "Delete Superman") and a matching `title` tooltip. They are reachable by Tab in order.
    - Test ids (if the e2e convention is used): `hero-list-view-button-{id}`, `hero-list-edit-button-{id}`, `hero-list-delete-button-{id}`, `hero-list-add-button`.
  - **Cards (mobile, `viewport.isMobile()`)**: one card per hero with the hero name, real name, superpower chips and power bar. The same **View / Edit / Delete** actions sit in a footer row, with Delete for admins only. "+ Add hero" stays at the top of the page and becomes full width.
  - **States** from the resource signals (the toolbar stays visible in every state):
    - `@if (heroes.isLoading())` shows skeleton rows.
    - `@else if (heroes.error())` shows an error panel with a **Retry** button (`heroes.reload()`).
    - `@else if (heroes.value()?.items.length === 0)` shows an empty state. With no search: "No heroes yet" with a "Register a hero" button to `/heroes/new`. With a search: "No heroes match '…'" with a "Clear search" button.
    - `@else`, the table or the cards.
  - **Paginator component** (`shared`), below the table: `page = model.required<number>()`, `totalPages = input.required<number>()` and `pageSize = input<number>()`. Prev and Next are disabled at the ends, and it shows "Page X of Y". Keyboard accessible.
  - Keep `HeroListComponent` thin: `HeroTableComponent` and `HeroCardComponent` are presentational, with `heroes = input.required<Hero[]>()`, `canDelete = input<boolean>()` and a `deleteRequested = output<Hero>()`. The list page owns the dialog and the API call.
- [x] **`HeroDetailComponent` (`/heroes/:id`):**
  - `id = input.required<number, string>({ transform: numberAttribute })` and `hero = heroResource(this.id)`.
  - Read-only view with audit info (created and updated by and at, with relative time through a pipe).
  - Edit button, and a Delete button `@if (auth.isAdmin())`, plus a "Back to heroes" link that keeps the list's query parameters (search, sort and page).
  - A 404 (`hero.not_found`) shows a "Hero not found" panel with a link back to the list.
- [x] **`HeroFormComponent` (`/heroes/new` and `/heroes/:id/edit`), with Signal Forms:**
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
  - A **Cancel** button returns to where the user came from (the list or the detail page), subject to the unsaved-changes guard.
  - **Unsaved-changes guard:** a functional `canDeactivate` that asks for confirmation when `heroForm().dirty()` and the form wasn't just saved (a `saved = signal(false)`).
- [x] **Delete** (admins only):
  - Available from the list's **Actions column** (table and cards) and from the detail page, visible only when `auth.isAdmin()`.
  - It opens the shared confirm dialog (`<dialog>`-based, with `open = model<boolean>()`): "Delete Superman? This hero will no longer appear in the registry." The buttons are **Cancel** (focused by default) and **Delete** (danger style, disabled while the request is pending).
  - On confirm: `heroApi.delete(id)`, then a toast ("Hero deleted"), then `heroes.reload()` on the list (if the page becomes empty, go back one page), or navigate to the list from the detail page.
  - A 403 is handled by T12's `errorInterceptor` toast. A 404 means it was already deleted, so reload.
- [x] **Performance:**
  - The form route and the chips input are lazy-loaded (`loadComponent`).
  - `@defer (on viewport)` for heavy, non-critical blocks (for example the audit timeline).
  - `track hero.id` in every `@for`.
- [x] **Accessibility:**
  - The table has a `<caption>` (visually hidden), headers use `scope="col"`, and the sorted column has `aria-sort`. Sortable headers are `<button>`s inside the `<th>`.
  - Icon-only action buttons have `aria-label`s that name the hero, and a visible focus ring.
  - Errors use `aria-describedby` (`FormFieldErrorComponent` from T13).
  - Focus returns to the list after a delete.

## Tests (TDD, Vitest, zoneless TestBed, `HttpTestingController`)
Call `TestBed.tick()` so a resource issues its request, then flush it and `await fixture.whenStable()`.

- [x] **`HeroApi`:**
  - `heroesResource` builds the right URL and query parameters from the query signal, and **refetches when the signal changes**.
  - `heroResource(undefined)` makes no request.
  - The writes use the correct verbs, URLs and bodies.
- [x] **`heroSchema`:**
  - Every rule's boundaries: name lengths 1/2/100/101, power level 0/1/100/101, a future birth date, more than 150 years ago, an invalid ZIP or state.
  - Superpowers: 0 and 11 items, duplicates ignoring case, and item length.
- [x] **`HeroListComponent`:**
  - The query parameter inputs drive the request.
  - Search is debounced (Vitest fake timers) and resets the page to 1.
  - The loading, error with retry, and empty states render. The empty state differs with and without a search. The toolbar is visible in every state.
  - **"+ Add hero"** is shown to a regular user and links to `/heroes/new`.
  - The table renders the columns in order (Hero name, Real name, Superpowers, Power, Created, Actions), one row per hero, and the "+N" superpower chip.
  - Clicking a sortable header writes `sortBy` to the URL, a second click toggles `sortDirection`, and `aria-sort` follows.
  - **Actions column:** View links to `/heroes/:id` and Edit to `/heroes/:id/edit`, with `aria-label`s naming the hero.
  - **Delete is hidden for regular users** and shown for admins. Clicking it opens the dialog naming the hero. Cancel calls nothing. Confirm calls delete, shows the toast and reloads. Deleting the last row of a page goes back one page.
  - On mobile (`isMobile()` true), cards render instead of the table, with the same actions.
- [x] **`HeroDetailComponent`:** loads by the `id` input, and a 404 shows the not-found panel.
- [x] **`HeroFormComponent`:**
  - Create mode starts empty. Edit mode fills from the resource through `linkedSignal`.
  - Submit sends the correct DTO (trimmed values, `superpowers` array).
  - A server 409 maps to `heroName`.
  - The `canDeactivate` guard asks when the form is dirty and not after a save.
- [x] **Chips input:** add, remove, trim, blocks duplicates, and the 10-item limit.

## Done when
- Every hero CRUD operation works end to end against the API, and the URL reflects search, sort and page.
- The layout is usable at 360 px and on desktop, and there are no console warnings.
- No manual `.subscribe()` in components, and no Reactive or template-driven forms. ESLint (T12 rules) passes.
- **TDD evidence:** every spec listed in Tests was red before its implementation (recorded in the Implementation notes). `npm run test:coverage` meets 80% lines and branches for `features/heroes` and the shared components.

## Implementation notes

### TDD evidence (red → green)
Each spec was written first and run on its own (`npm test -- --watch=false --include=<path>`) before the code existed. "Compile" means the build failed because the unit did not exist yet (`TS2307`).

| # | Unit | Specs | Red run | Green |
| - | ---- | ----- | ------- | ----- |
| 1 | `hero-schema` (every rule's boundaries, superpowers, states, ZIP, model mapping) | 50 | 50 failed (compile) | 50/50 |
| 2 | `shared/relative-time` (function and pipe) | 10 | 10 failed (compile) | 10/10 |
| 3 | `HeroApi` (resource URLs and parameters, refetch on signal change, no request for an undefined id, write verbs and bodies) | 8 | 8 failed (compile) | 8/8 |
| 4 | `shared` chips input, paginator and confirm dialog | 12 + 4 + 7 | 23 failed (compile) | 23/23 (see the fixes below) |
| 5 | `HeroTable` + `HeroCard` (columns, "+N" chip, meter, sort headers and `aria-sort`, actions, admin-only Delete) | 12 + 3 | 15 failed (compile) | 15/15 |
| 6 | `HeroList` (query inputs, debounced search, states, toolbar, sort and page in the URL, delete flow, mobile cards) | 26 | 26 failed (compile: `HeroListMemory` missing) | 26/26 (see the spec fixes below) |
| 7 | `HeroDetail` (load by id, 404 panel, back link, deferred audit, admin delete) | 10 | 10 failed (compile) | 10/10 |
| 8 | `HeroForm` + `unsavedChangesGuard` | 18 + 3 | 21 failed (compile) | 21/21 (see the bug and spec fixes below) |
| 9 | Chips input: invalid style only once touched (found in the browser) | +1 | 1 failed: `expected 'true' to be 'false'` | 13/13 |
| 10 | Chips input: text cleared after a fast Enter (found in the browser) | +1 | 1 failed: `expected 'Weather control' to be ''` | 14/14 |
| 11 | List delete: focus moves only after the modal closes (found in the browser) | 1 changed | 1 failed: `expected [ true ] to deeply equal [ false ]` | 26/26 |

Final run: **30 files, 353 tests passed**. Coverage of `core`, `features`, `shared` and `layout`: 97.9% lines and 93.4% branches. `npm run lint` and `npm run build` are clean (the form route and chips input are a lazy chunk), and Prettier is clean. There is no `.subscribe()` in application code, and no Reactive or template-driven forms.

### Bugs found by the specs (the code was wrong)
- **Edit mode with a 404 crashed.** `httpResource.value()` throws while the resource is in its error state, and `linkedSignal` and the title read it. Both now read it through `hasValue()`. The list's computeds were hardened the same way.

### Spec and compile fixes on the way to green (the spec or tooling was wrong, not the behavior)
- **`hero-schema`:** the 257-character email case asserted `maxLength` only, but its 245-character local part is also an invalid email. Before any implementation, it was changed to assert that `maxLength` is present.
- **`ConfirmDialog`:**
  - `viewChild` cannot target ES `#private` members (NG1053), so those became TypeScript `private`.
  - jsdom implements neither `showModal()` nor `close()`, so both have an attribute fallback.
- **`ChipsInput`:**
  - Renamed `max` to `limit`, because `[formField]` drives a control's `max` input from the field's MAX metadata.
  - In spec 9, the test host used `required()`, which treats `[]` as a value. It now uses a `validate` rule.
- **`HeroForm`:**
  - Signal Forms forbids hand-written `min`/`max` on `[formField]` inputs (NG8022). The schema's `min(1)` / `max(100)` set them.
  - The spec helper for `<select>` dispatched only `change`; a browser also fires `input`, which `[formField]` listens to.
- **`HeroList`:**
  - The loading and delete specs awaited `whenStable()` while a request was deliberately pending (a deadlock). They now poll with `vi.waitFor`.
  - `afterEach` must restore real timers *before* `verify()`, or a failure leaks fake timers into the next test.
  - The debounce specs fake only `setInterval`, `clearInterval` and `Date`, which is what rxjs `debounceTime` uses, and do it *before* rendering.
- **`hero()` builder:** moved to `src/testing/heroes.ts`, so importing it no longer re-runs another spec file.

### Bugs found in the browser (fixed test-first, rows 9 to 11)
- **The chips box was red before it was touched.** `[formField]` passes `invalid` immediately, so the control now also takes `touched` and shows the error state only once the field is touched, like every other input.
- **Fast typing (or a paste) followed by Enter added a chip but left the text in the box**, so the next word was appended ("Weather controlFlight"). A `[value]="text()"` binding still held its last *rendered* `''`, so setting `''` was not a change. The input element is now the only source of the typed text, read and cleared directly.
- **After a delete from the list, focus landed on `<body>`.** The heading was focused while the modal `<dialog>` still made the page inert. Focus now moves in `afterNextRender`, after the render that closes the dialog. jsdom has no inertness, so the spec records whether the dialog was open at the moment of focusing.

### Decisions and deviations from the steps
- **States:** the select and the rule accept the same **56** codes as the server's `UsStateCode` (50 states, DC and 5 territories), not only 50 + DC, so the UI never rejects what the API accepts.
- **Superpowers:** one array-level `validate` (count, item length and case-insensitive duplicates) instead of `applyEach`. This puts one message under the chips, as the server reports one message for the list.
- **Birth date:** `validate` rules on the ISO string, with today in UTC like the server, instead of `maxDate` (which works on `Date` values).
- **Accessibility:** "Back to heroes" (detail) and Cancel (create) return to the list's last search, sort and page through `HeroListMemory`, which is in memory, so a fresh tab goes to page 1.
- **Sorting and paging:** changing the search or the sort resets the page (`page` is removed from the URL). The paginator is hidden when everything fits on one page.
- **Unsaved changes:** `unsavedChangesGuard` (in `shared/forms`, reusable) uses the browser's own `confirm()`.
- **Performance:** the audit trail uses `@defer (on viewport)`; the form route (with the chips input) and the other pages are lazy; every `@for` tracks by id.
- **File names** follow the style guide: `hero-models.ts`, `hero-api.ts`, `hero-schema.ts`. `PagedResult` lives in `core/api`.
- **Test ids:** the ones listed above (`hero-list-add-button`, `hero-list-{view,edit,delete}-button-{id}`) are on the elements, for e2e.

### Smoke test (Chrome, real API through the dev proxy)
- **List (admin):** the three seeded heroes, with "+N" chips, power meters and the actions column.
  - Sorting by Power and then toggling it updated the order, `aria-sort` and the URL (`sortBy=PowerLevel&sortDirection=Descending`).
  - Typing "flight" wrote `search=flight` to the URL after the debounce ("1 hero found": Superman). A search with no match showed the "No heroes match" state, and Clear search restored the list while keeping the sort.
- **Create:** typed into the real form:
  - text fields, the native date picker, and the chips (Enter and comma; a duplicate was refused with a message);
  - power 88 (the slider followed the number), and the state chosen with the keyboard.
  - It saved to `/heroes/4` with every field, and the audit trail read "Created just now by user #1".
- **Edit:** loaded as "Edit Storm".
  - Renaming to an existing hero gave the 409 with the server message under Hero name (`aria-invalid`, focused). Editing cleared it.
  - Saving showed "Hero saved." and the detail page.
- **Delete (admin):**
  - From the detail page, the dialog named the hero and focused Cancel. Delete showed the toast and went back to the list.
  - From the list: the toast, the reload, and focus on the "Heroes" heading.
- **Regular user (`smoke.t13`):** "+ Add hero" is visible and links to `/heroes/new`. Each row has only View and Edit, and the detail page has Edit but no Delete.
- **360 px:** checked in a same-origin iframe, because the window cannot be resized.
  - The list renders cards with a full-width "+ Add hero" and the menu toggle.
  - The detail page overflowed (439 px) because the field grid's value column could not shrink below a long value. Fixed in CSS (styles are not unit-tested): the label goes above the value on small screens, with `minmax(0, 1fr)` and `overflow-wrap: anywhere`. Afterwards no element reached past the visible width on the list, detail, edit or new pages.
- **Console:** no application errors or warnings. The remaining entries are dev-only:
  - the view-transition messages from T12 (pages loaded hidden, such as the iframes, and one transition cut short by fast scripted navigation);
  - NG0751, Angular's HMR info log for `@defer`.
- **Test data:** the smoke heroes "Storm" (renamed "Storm Prime"), "Zeta Smoke" and "Zeta Smoke Two" were created and then deleted. They remain in the database as soft-deleted rows.

