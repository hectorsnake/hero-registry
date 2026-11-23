import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { hero } from '../../../../testing/heroes';
import { AuthService } from '../../../core/auth/auth-service';
import { ToastService } from '../../../core/ui/toast-service';
import { ViewportService } from '../../../core/ui/viewport-service';
import { HeroListMemory } from '../hero-list-memory';
import { Hero } from '../hero-models';
import { HeroList } from './hero-list';

type Inputs = Partial<Record<'search' | 'page' | 'sortBy' | 'sortDirection', string>>;

describe('HeroList', () => {
  const isAdmin = signal(false);
  const isMobile = signal(false);
  let backend: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    isAdmin.set(false);
    isMobile.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAdmin: isAdmin.asReadonly() } },
        { provide: ViewportService, useValue: { isMobile: isMobile.asReadonly() } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    vi.useRealTimers(); // first: a failed verify() must not leave fake timers for the next test
    backend.verify();
  });

  const twoHeroes = [
    hero(),
    hero({ id: 8, heroName: 'Spider-Man', firstName: 'Peter', lastName: 'Parker' }),
  ];

  function heroesRequest(): TestRequest {
    return backend.expectOne((r) => r.url === '/api/heroes' && r.method === 'GET');
  }

  /** The list's reload after a delete (the delete resolves asynchronously, so poll for it). */
  function reloadRequest(): Promise<TestRequest> {
    return vi.waitFor(() => {
      TestBed.tick();
      return heroesRequest();
    });
  }

  function page(items: Hero[], { page = 1, totalCount = items.length } = {}) {
    return { items, page, pageSize: 10, totalCount };
  }

  /** Creates the list with the given query-string inputs; the first request is left pending. */
  async function create(inputs: Inputs = {}) {
    const fixture = TestBed.createComponent(HeroList);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    TestBed.tick();
    return view(fixture);
  }

  /** Creates the list and answers its first request. */
  async function render(inputs: Inputs = {}, items: Hero[] = twoHeroes, paging = {}) {
    const v = await create(inputs);
    heroesRequest().flush(page(items, paging));
    await v.fixture.whenStable();
    return v;
  }

  function view(fixture: ComponentFixture<HeroList>) {
    const element = fixture.nativeElement as HTMLElement;
    const button = (name: string) =>
      Array.from(element.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === name || b.getAttribute('aria-label') === name,
      );
    const searchBox = () => element.querySelector<HTMLInputElement>('input[type="search"]')!;
    return { fixture, element, button, searchBox, text: () => element.textContent ?? '' };
  }

  function navigatedWith(queryParams: Record<string, unknown>) {
    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams, queryParamsHandling: 'merge' }),
    );
  }

  describe('query', () => {
    it('asks for page 1 of 10 heroes sorted by name by default', async () => {
      await create();

      const params = heroesRequest().request.params;
      expect(params.get('page')).toBe('1');
      expect(params.get('pageSize')).toBe('10');
      expect(params.get('sortBy')).toBe('HeroName');
      expect(params.get('sortDirection')).toBe('Ascending');
      expect(params.has('search')).toBe(false);
    });

    it('takes search, page and sort from the URL (route inputs)', async () => {
      await create({
        search: 'flight',
        page: '2',
        sortBy: 'PowerLevel',
        sortDirection: 'Descending',
      });

      const params = heroesRequest().request.params;
      expect(params.get('search')).toBe('flight');
      expect(params.get('page')).toBe('2');
      expect(params.get('sortBy')).toBe('PowerLevel');
      expect(params.get('sortDirection')).toBe('Descending');
    });

    it('ignores an unknown sort in the URL', async () => {
      await create({ sortBy: 'Email', sortDirection: 'Sideways' });

      const params = heroesRequest().request.params;
      expect(params.get('sortBy')).toBe('HeroName');
      expect(params.get('sortDirection')).toBe('Ascending');
    });

    it('remembers the query for the detail page "Back to heroes" link', async () => {
      await render({ search: 'flight', page: '2' });

      expect(TestBed.inject(HeroListMemory).queryParams()).toEqual({
        search: 'flight',
        page: 2,
        sortBy: 'HeroName',
        sortDirection: 'Ascending',
      });
    });
  });

  describe('search', () => {
    // rxjs debounceTime runs on setInterval and Date.now; Angular's scheduler keeps real timers.
    beforeEach(() => vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] }));

    it('writes the search to the URL after 300 ms, back on page 1', async () => {
      const v = await render();

      v.searchBox().value = 'spider';
      v.searchBox().dispatchEvent(new Event('input'));
      TestBed.tick();
      vi.advanceTimersByTime(299);
      TestBed.tick();
      expect(router.navigate).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      TestBed.tick();
      navigatedWith({ search: 'spider', page: null });
    });

    it('only the last of several quick keystrokes is sent', async () => {
      const v = await render();

      for (const text of ['s', 'sp', 'spi']) {
        v.searchBox().value = text;
        v.searchBox().dispatchEvent(new Event('input'));
        TestBed.tick();
        vi.advanceTimersByTime(100);
      }
      vi.advanceTimersByTime(300);
      TestBed.tick();

      expect(router.navigate).toHaveBeenCalledTimes(1);
      navigatedWith({ search: 'spi', page: null });
    });

    it('clearing the box removes the search from the URL', async () => {
      const v = await render({ search: 'spider' });

      v.searchBox().value = '';
      v.searchBox().dispatchEvent(new Event('input'));
      TestBed.tick();
      vi.advanceTimersByTime(300);
      TestBed.tick();

      navigatedWith({ search: null, page: null });
    });

    it('shows the search from the URL in the box', async () => {
      const v = await render({ search: 'flight' });

      expect(v.searchBox().value).toBe('flight');
    });
  });

  describe('states (the toolbar stays visible in every one)', () => {
    function toolbarVisible(element: HTMLElement): void {
      expect(element.querySelector('h1')?.textContent?.trim()).toBe('Heroes');
      expect(element.querySelector('[data-testid="hero-list-add-button"]')).not.toBeNull();
      expect(element.querySelector('input[type="search"]')).not.toBeNull();
    }

    it('shows skeleton rows while loading', async () => {
      const v = await create(); // the request stays pending: awaiting stability would wait for it forever
      TestBed.tick();

      expect(v.element.querySelector('[aria-busy="true"]')?.textContent).toContain(
        'Loading heroes',
      );
      toolbarVisible(v.element);
      heroesRequest().flush(page([]));
    });

    it('shows an error with a Retry button that reloads', async () => {
      const v = await create();
      heroesRequest().flush(
        { title: 'Boom', status: 500 },
        { status: 500, statusText: 'Server Error' },
      );
      await v.fixture.whenStable();

      expect(v.text()).toContain("Couldn't load the heroes");
      toolbarVisible(v.element);

      v.button('Retry')!.click();
      TestBed.tick();
      heroesRequest().flush(page(twoHeroes));
    });

    it('without a search, the empty state invites to register the first hero', async () => {
      const v = await render({}, []);

      expect(v.text()).toContain('No heroes yet');
      expect(
        v.element.querySelector('a[href="/heroes/new"][data-empty-action]')?.textContent?.trim(),
      ).toBe('Register a hero');
      toolbarVisible(v.element);
    });

    it('with a search, the empty state names it and offers to clear it', async () => {
      const v = await render({ search: 'zorro' }, []);

      expect(v.text()).toContain("No heroes match 'zorro'");
      v.button('Clear search')!.click();

      navigatedWith({ search: null, page: null });
    });
  });

  describe('toolbar', () => {
    it('shows "+ Add hero" to a regular user, linking to /heroes/new', async () => {
      const v = await render();

      const add = v.element.querySelector<HTMLAnchorElement>(
        '[data-testid="hero-list-add-button"]',
      )!;
      expect(add.getAttribute('href')).toBe('/heroes/new');
      expect(add.textContent).toContain('Add hero');
    });

    it('announces the number of heroes found', async () => {
      const v = await render({}, twoHeroes, { totalCount: 12 });

      expect(v.element.querySelector('[aria-live="polite"]')?.textContent?.trim()).toBe(
        '12 heroes found',
      );
    });

    it('uses the singular for one hero', async () => {
      const v = await render({}, [hero()]);

      expect(v.element.querySelector('[aria-live="polite"]')?.textContent?.trim()).toBe(
        '1 hero found',
      );
    });
  });

  describe('table', () => {
    it('renders one row per hero', async () => {
      const v = await render();

      expect(v.element.querySelectorAll('app-hero-table tbody tr')).toHaveLength(2);
    });

    it('a header click writes the sort to the URL and goes back to page 1', async () => {
      const v = await render({ page: '3' }, twoHeroes, { page: 3, totalCount: 30 });

      const power = Array.from(v.element.querySelectorAll('th')).find((th) =>
        th.textContent?.includes('Power'),
      )!;
      power.querySelector('button')!.click();

      navigatedWith({ sortBy: 'PowerLevel', sortDirection: 'Ascending', page: null });
    });

    it('a second click on the sorted column toggles the direction (aria-sort follows the URL)', async () => {
      const v = await render({ sortBy: 'PowerLevel', sortDirection: 'Ascending' });
      const power = () =>
        Array.from(v.element.querySelectorAll('th')).find((th) =>
          th.textContent?.includes('Power'),
        )!;
      expect(power().getAttribute('aria-sort')).toBe('ascending');

      power().querySelector('button')!.click();

      navigatedWith({ sortBy: 'PowerLevel', sortDirection: 'Descending', page: null });
    });

    it('the paginator writes the page to the URL', async () => {
      const v = await render({}, twoHeroes, { totalCount: 25 });

      v.button('Next page')!.click();

      navigatedWith({ page: 2 });
    });
  });

  describe('delete', () => {
    it('is hidden from regular users', async () => {
      const v = await render();

      expect(v.element.querySelector('[data-testid^="hero-list-delete-button"]')).toBeNull();
    });

    async function openDelete() {
      isAdmin.set(true);
      const v = await render();
      v.element
        .querySelector<HTMLButtonElement>('[data-testid="hero-list-delete-button-7"]')!
        .click();
      await v.fixture.whenStable();
      return v;
    }

    it('opens the confirmation naming the hero', async () => {
      const v = await openDelete();

      expect(v.element.querySelector('dialog')?.open).toBe(true);
      expect(v.element.querySelector('dialog')?.textContent).toContain('Delete Superman?');
    });

    it('Cancel deletes nothing', async () => {
      const v = await openDelete();

      v.button('Cancel')!.click();
      await v.fixture.whenStable();

      backend.expectNone((r) => r.method === 'DELETE');
      expect(v.element.querySelector('dialog')?.open).toBe(false);
    });

    it('Delete removes the hero, shows a toast, reloads and returns focus to the list', async () => {
      const v = await openDelete();
      // A modal dialog makes the page inert: focusing the heading only works once the dialog is closed.
      // jsdom has no inertness, so record whether the dialog was still open when focus moved.
      const dialogOpenWhenFocused: boolean[] = [];
      const heading = v.element.querySelector('h1')!;
      const focus = heading.focus.bind(heading);
      heading.focus = (options?: FocusOptions) => {
        dialogOpenWhenFocused.push(v.element.querySelector('dialog')!.open);
        focus(options);
      };

      v.element.querySelector<HTMLButtonElement>('dialog button[data-danger]')!.click();
      backend
        .expectOne((r) => r.method === 'DELETE' && r.url === '/api/heroes/7')
        .flush(null, {
          status: 204,
          statusText: 'No Content',
        });
      (await reloadRequest()).flush(page([twoHeroes[1]]));
      await v.fixture.whenStable();

      expect(
        TestBed.inject(ToastService)
          .toasts()
          .map((t) => t.text),
      ).toEqual(['Hero deleted.']);
      expect(v.element.querySelector('dialog')?.open).toBe(false);
      expect(document.activeElement).toBe(v.element.querySelector('h1'));
      expect(dialogOpenWhenFocused).toEqual([false]);
    });

    it('deleting the last hero of a page goes back one page', async () => {
      isAdmin.set(true);
      const v = await render({ page: '3' }, [hero()], { page: 3, totalCount: 21 });
      v.element
        .querySelector<HTMLButtonElement>('[data-testid="hero-list-delete-button-7"]')!
        .click();
      await v.fixture.whenStable();

      v.element.querySelector<HTMLButtonElement>('dialog button[data-danger]')!.click();
      backend
        .expectOne((r) => r.method === 'DELETE')
        .flush(null, { status: 204, statusText: 'No Content' });

      await vi.waitFor(() => navigatedWith({ page: 2 }));
      backend.expectNone((r) => r.url === '/api/heroes'); // the new URL loads the previous page
    });

    it('a hero that was already deleted (404) just refreshes the list', async () => {
      const v = await openDelete();

      v.element.querySelector<HTMLButtonElement>('dialog button[data-danger]')!.click();
      backend
        .expectOne((r) => r.method === 'DELETE')
        .flush(
          { title: 'Not Found', status: 404, code: 'hero.not_found' },
          { status: 404, statusText: 'Not Found' },
        );

      (await reloadRequest()).flush(page([twoHeroes[1]]));
      expect(
        TestBed.inject(ToastService)
          .toasts()
          .map((t) => t.text),
      ).toEqual(['That hero was already deleted.']);
    });
  });

  describe('on mobile', () => {
    it('renders cards with the same actions instead of the table', async () => {
      isMobile.set(true);
      isAdmin.set(true);

      const v = await render();

      expect(v.element.querySelector('app-hero-table')).toBeNull();
      expect(v.element.querySelectorAll('app-hero-card')).toHaveLength(2);
      expect(
        v.element.querySelector('app-hero-card button[aria-label="Delete Superman"]'),
      ).not.toBeNull();
    });
  });
});
