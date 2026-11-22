---
name: angular-unit-test
description: Write or fix Angular unit tests (*.spec.ts) for components, API services and resources (HttpTestingController), stateful services, interceptors, guards, Signal Forms schemas, pipes, directives and plain functions. Works with Vitest (@angular/build:unit-test) or Karma + Jasmine, detected from the project. Zoneless-aware (no fakeAsync), test-first (red → green → refactor) when the project requires TDD, queries the DOM by data-testid when the project uses test ids, and uses component harnesses for UI libraries that provide them. Use whenever an Angular unit of logic is created or changed (the angular-ui skill requires it), when the user asks for unit tests, coverage, or a failing ng test.
---

# Angular unit tests (generic, zoneless)

## 1. Detect the setup first
| Question | Where to look |
| --- | --- |
| Runner | `angular.json` → `architect.test.builder`: `@angular/build:unit-test` (Vitest, default from Angular 21) or `@angular/build:karma` (Karma + Jasmine). Also `package.json` devDependencies |
| Scripts | `package.json` → `test`, `test:ci`, `test:coverage` |
| Zoneless? | No `zone.js` in `package.json` / `polyfills` → zoneless |
| Test helpers | `src/testing/` (for example test-id helpers), shared builders |
| TDD / coverage rules | CLAUDE.md, task docs, coverage thresholds in the test configuration |

Every component, service, interceptor, guard, pipe, directive, schema and exported function with logic gets
a `<file>.spec.ts` next to it, with the same name, in the same folder.

## 2. TDD loop (when the project requires TDD)
1. **Red:** write the spec for one behavior first. Run only that spec. It must fail **for the right reason**:
   an assertion failure, or a compile error because the symbol doesn't exist yet. A spec that passes on its
   first run proves nothing; fix the spec.
2. **Green:** write the minimum code that makes it pass. Run it again.
3. **Refactor** with the tests green, then the next behavior.
4. Work inside-out: pure functions and schemas → services → interceptors and guards → components.
5. **Report the evidence** (for example "ProductApi: 6 specs written first, 6 failed → green") where the
   project records it (task notes, PR description).

## 3. Running
| Goal | Vitest (`@angular/build:unit-test`) | Karma + Jasmine |
| --- | --- | --- |
| All specs, single run | `npx ng test --watch=false` | `npx ng test --no-watch --browsers=ChromeHeadless` |
| One folder or file | `npx ng test --watch=false --include=src/app/products` | same `--include` (+ `--no-watch --browsers=ChromeHeadless`) |
| Filter by name | `npx ng test --watch=false --filter="ProductList"` | `--filter` on recent versions |
| Coverage | `npx ng test --watch=false --coverage` (needs `@vitest/coverage-v8`) | `--coverage` → `coverage/` |
| Watch | `npm test` | `npm test` |
Prefer the project's scripts when they exist.

## 4. Rules
- **Zoneless: `fakeAsync` / `tick` / `waitForAsync` don't work** (they need zone.js).
  - `await fixture.whenStable()` after creating the component, after `setInput`, and after any action.
  - `TestBed.tick()` to flush effects and let an `httpResource` / `resource` issue its request.
  - Timers: Vitest `vi.useFakeTimers()` / `vi.advanceTimersByTime()` / `vi.useRealTimers()`;
    Jasmine `jasmine.clock().install()` / `tick()` / `uninstall()`.
- **Test behavior through the public surface:** DOM, inputs/outputs, public methods, signals exposed
  read-only, HTTP calls. `#private` members can't be reached and `protected` ones shouldn't be.
- **Query the DOM** by test id when the project has the convention (`byTestId(fixture, 'product-form-name-input')`),
  otherwise by role, label or text. Never by CSS classes or tag paths. If the project has a
  "missing test ids" helper, every component spec runs it after the main state has rendered.
- **UI library components:** interact through **component harnesses** when the library provides them (for
  Angular Material: `TestbedHarnessEnvironment.loader(fixture)`, `documentRootLoader` for overlays).
- **Signal inputs:** `fixture.componentRef.setInput('product', value)`, then `await fixture.whenStable()`.
- **Outputs:** `fixture.componentInstance.saved.subscribe(spy)`, or test through a host component.
- **Signals:** assert signal values directly (`expect(service.isAdmin()).toBe(true)`); for forms, assert the
  field state signals (`form.name().errors()`, `form().invalid()`).
- **HTTP:**
  - Never hit a real API. `provideHttpClient(), provideHttpClientTesting()`; override any base-URL token.
  - **API services and resources:** assert method, URL, query params and body with `HttpTestingController`
    (`expectOne`, `req.request.params.get(...)`), flush realistic DTOs, `httpTesting.verify()` in `afterEach`.
    For a resource: create it in an injection context (`TestBed.runInInjectionContext`), `TestBed.tick()`,
    then `expectOne`, `flush`, and `await` stability before asserting `value()`.
  - **Components:** prefer a fake of the API service (`{ provide: ProductApi, useValue: fake }`) returning
    `of(...)` / `throwError(...)`; use `HttpTestingController` when the component owns the `httpResource`.
- **Interceptors:** `provideHttpClient(withInterceptors([interceptor]))` + `HttpTestingController`; assert
  headers added or not, retries (one retry, never a loop), and redirects via a `Router` spy.
- **Guards:** `TestBed.runInInjectionContext(() => guard(route, state))`; assert `true` or the `UrlTree`.
- **Router:** `provideRouter([])`; for routed components with `withComponentInputBinding()`, use
  `RouterTestingHarness.create()` + `harness.navigateByUrl('/products/5', ProductDetail)`. Assert navigation
  with a spy on `Router.navigate` / `navigateByUrl`.
- **Component-level providers:** replace them with `TestBed.overrideComponent(X, { set: { providers: [...] } })`.
- **Storage hygiene:** when the project forbids tokens in web storage, spy on `localStorage` /
  `sessionStorage` `setItem` and assert it is never called.

## 5. What to cover
- **Components:** renders loaded data; loading, empty and error states (with retry); each user action
  produces the expected call, output or navigation; validation messages including server 400 `errors` mapped to
  fields and 409/404 messages; disabled states (submit while pending or invalid); test-id check if applicable.
- **API services / resources:** one test per method or resource (URL, params, body); empty filters omitted;
  errors propagate; resources refetch when their signal parameters change and skip the request when the
  parameter is `undefined`.
- **Stateful services:** initial state; each action changes the state; selectors recompute.
- **Form schemas:** every rule's boundaries (min - 1, min, max, max + 1), cross-field rules, server-error mapping.
- **Pure functions and pipes:** table-driven cases with edge cases (null, empty, whitespace, accents, padding).

## 6. Style
- `describe` / `it` text in English, as behavior ("shows an error when the name is taken").
- Arrange / act / assert, separated by blank lines. One behavior per `it`.
- Build test data with small builder functions in the spec (`product({ name: 'X' })`), not large shared fixtures.
- Same formatting as app code (the project's Prettier / EditorConfig).

## 7. Templates (Vitest syntax; Jasmine equivalents: `jasmine.createSpyObj`, `and.returnValue`, `toBeFalse()`)

**Component**
```ts
describe('ProductList', () => {
  const api = { delete: vi.fn() };

  beforeEach(async () => {
    api.delete.mockReturnValue(of(undefined));
    await TestBed.configureTestingModule({
      imports: [ProductList],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: ProductApi, useValue: api }],
    }).compileComponents();
  });

  async function render() {
    const fixture = TestBed.createComponent(ProductList);
    TestBed.tick();
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/products').flush(page([product({ id: 1, name: 'Lamp' })]));
    await fixture.whenStable();
    return fixture;
  }

  it('shows one row per product', async () => {
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('Lamp');
  });
});
```

**API service**
```ts
describe('ProductApi', () => {
  let api: ProductApi;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ProductApi);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('posts the new product', async () => {
    const result = firstValueFrom(api.create({ name: 'Lamp', price: 10 }));

    const req = httpTesting.expectOne('/api/products');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Lamp', price: 10 });
    req.flush(product({ id: 7 }));

    expect((await result).id).toBe(7);
  });
});
```

## 8. Done means
- A spec exists for every new or changed file with logic (and the test-id check where the project uses it).
- With TDD required: each spec was red before its implementation, and the evidence is reported.
- The full single-run test command passes; report the executed / passed counts. Coverage meets the project's
  thresholds when it has them. If a spec is intentionally left failing, say so with its output.
- No focused tests (`fdescribe`/`fit`, `describe.only`/`it.only`), and no skipped tests (`xit`, `it.skip`)
  without a TODO giving the reason.
