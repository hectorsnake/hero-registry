import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { DeferBlockState, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { hero } from '../../../../testing/heroes';
import { AuthService } from '../../../core/auth/auth-service';
import { ToastService } from '../../../core/ui/toast-service';
import { HeroListMemory } from '../hero-list-memory';
import { HeroDetail } from './hero-detail';

describe('HeroDetail', () => {
  const isAdmin = signal(false);
  let backend: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    isAdmin.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAdmin: isAdmin.asReadonly() } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => backend.verify());

  async function create(id = '7') {
    const fixture = TestBed.createComponent(HeroDetail);
    fixture.componentRef.setInput('id', id); // route params arrive as strings
    TestBed.tick();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element, text: () => element.textContent ?? '' };
  }

  async function render(body = hero({ updatedBy: 2, updatedAt: '2026-11-20T10:00:00Z' })) {
    const v = await create();
    backend.expectOne('/api/heroes/7').flush(body);
    await v.fixture.whenStable();
    return v;
  }

  it('loads the hero of the id input', async () => {
    const v = await create('7');

    expect(backend.expectOne('/api/heroes/7').request.method).toBe('GET');
    expect(v.text()).toContain('Loading');
  });

  it('shows every field, read-only', async () => {
    const { element, text } = await render();

    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Superman');
    for (const value of [
      'Clark Kent',
      'clark@dailyplanet.com',
      '1978-06-18',
      '344 Clinton St',
      'Metropolis',
      'NY',
      '10001',
    ]) {
      expect(text()).toContain(value);
    }
    expect(element.querySelectorAll('[data-chip]').length).toBe(4); // every superpower, no "+N"
    expect(element.querySelector('[role="meter"]')?.getAttribute('aria-valuenow')).toBe('100');
    expect(element.querySelector('input, textarea, select')).toBeNull();
  });

  it('shows the audit trail (deferred until scrolled into view)', async () => {
    const v = await render();

    const [audit] = await v.fixture.getDeferBlocks();
    await audit.render(DeferBlockState.Complete);

    expect(v.text()).toMatch(/Created .* by user #1/);
    expect(v.text()).toMatch(/Updated .* by user #2/);
  });

  it('a hero never updated says so', async () => {
    const v = await render(hero());

    const [audit] = await v.fixture.getDeferBlocks();
    await audit.render(DeferBlockState.Complete);

    expect(v.text()).toContain('Never updated');
  });

  it('links to the edit page', async () => {
    const { element } = await render();

    expect(element.querySelector('a[href="/heroes/7/edit"]')?.textContent).toContain('Edit');
  });

  it('"Back to heroes" returns to the list the user came from', async () => {
    TestBed.inject(HeroListMemory).remember({ search: 'flight', page: 2 });

    const { element } = await render();

    const back = Array.from(element.querySelectorAll('a')).find((a) =>
      a.textContent?.includes('Back to heroes'),
    )!;
    expect(back.getAttribute('href')).toBe('/heroes?search=flight&page=2');
  });

  it('a 404 shows "Hero not found" with a link back to the list', async () => {
    const v = await create('99');
    backend
      .expectOne('/api/heroes/99')
      .flush(
        { title: 'Not Found', status: 404, code: 'hero.not_found' },
        { status: 404, statusText: 'Not Found' },
      );
    await v.fixture.whenStable();

    expect(v.text()).toContain('Hero not found');
    expect(v.element.querySelector('a[href="/heroes"]')).not.toBeNull();
    expect(v.element.querySelector('a[href="/heroes/99/edit"]')).toBeNull();
  });

  it('another error offers a retry', async () => {
    const v = await create();
    backend
      .expectOne('/api/heroes/7')
      .flush({ title: 'Boom', status: 500 }, { status: 500, statusText: 'Error' });
    await v.fixture.whenStable();

    Array.from(v.element.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Retry')!
      .click();
    TestBed.tick();

    backend.expectOne('/api/heroes/7').flush(hero());
  });

  describe('delete', () => {
    it('is hidden from regular users', async () => {
      const { element } = await render();

      expect(element.querySelector('button[aria-label="Delete Superman"]')).toBeNull();
    });

    it('admins confirm, then the hero is deleted and the list is shown', async () => {
      isAdmin.set(true);
      TestBed.inject(HeroListMemory).remember({ page: 2 });
      const v = await render();

      v.element.querySelector<HTMLButtonElement>('button[aria-label="Delete Superman"]')!.click();
      await v.fixture.whenStable();
      expect(v.element.querySelector('dialog')?.textContent).toContain('Delete Superman?');
      v.element.querySelector<HTMLButtonElement>('dialog button[data-danger]')!.click();
      backend
        .expectOne((r) => r.method === 'DELETE' && r.url === '/api/heroes/7')
        .flush(null, {
          status: 204,
          statusText: 'No Content',
        });

      await vi.waitFor(() =>
        expect(router.navigate).toHaveBeenCalledWith(['/heroes'], { queryParams: { page: 2 } }),
      );
      expect(
        TestBed.inject(ToastService)
          .toasts()
          .map((t) => t.text),
      ).toEqual(['Hero deleted.']);
    });
  });
});
