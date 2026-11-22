# T14: Angular hero management (list, detail, form and delete)

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** UI · **Estimate:** 10 h · **Depends on:** T10, T12

## Goal
Provide a responsive, user-friendly UI for all hero CRUD operations.

## Steps
- [ ] `HeroApiService`: `search(query)`, `get(id)`, `create(dto)`, `update(id, dto)` and `delete(id)`, with typed models (`Hero`, `HeroQuery`, `PagedResult<T>`).
- [ ] `HeroStore`, a signal-based feature state: query, items, total, loading and error. Keep the query in sync with URL query parameters so the list is shareable and the back button works.
- [ ] `HeroListComponent` (`/heroes`):
  - Search box (debounced at 300 ms), sorting (HeroName, PowerLevel, CreatedAt) and a paginator.
  - Table on desktop, cards on mobile.
  - Shows superpowers as chips and the power level as a bar or badge.
  - Empty, loading and error states.
- [ ] `HeroDetailComponent` (`/heroes/:id`): read-only view with the audit info (created and updated by and at). Has Edit and Delete actions.
- [ ] `HeroFormComponent` (`/heroes/new` and `/heroes/:id/edit`):
  - Reactive typed form for all fields.
  - Superpowers chip input (add and remove, 1 to 10, no duplicates).
  - BirthDate date picker (no future dates).
  - PowerLevel slider or number input (1 to 100).
  - US State select and ZipCode pattern.
  - Validators mirror the server. Server 400 and 409 errors are mapped to the fields.
  - Unsaved-changes guard (`canDeactivate`).
- [ ] Delete: visible only when `isAdmin()`. Uses the confirm dialog. On success, shows a toast and refreshes the list.
- [ ] Use `@for` with `track`, `@if`, the `OnPush` strategy and no subscriptions without cleanup (`takeUntilDestroyed` or signals).

## Tests (TDD)
- [ ] `HeroApiService` builds the correct URLs, query parameters and verbs (`HttpTestingController`).
- [ ] `HeroStore` updates state on success and on error.
- [ ] `HeroFormComponent` validators, including boundaries and superpower duplicates. Edit mode patches values. Submit sends the correct DTO.
- [ ] `HeroListComponent` hides the delete action for non-admins and calls delete after confirmation.

## Done when
- Every hero CRUD operation works end to end against the API, the layout is usable at 360 px and on desktop, and there are no console warnings.
