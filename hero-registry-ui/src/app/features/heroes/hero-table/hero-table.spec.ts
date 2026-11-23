import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { hero } from '../../../../testing/heroes';
import { Hero, HeroSort } from '../hero-models';
import { HeroTable } from './hero-table';

describe('HeroTable', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  async function render(
    heroes: Hero[] = [
      hero(),
      hero({
        id: 8,
        heroName: 'Spider-Man',
        firstName: 'Peter',
        lastName: 'Parker',
        superpowers: ['Agility'],
      }),
    ],
    {
      canDelete = false,
      sort = { sortBy: 'HeroName', sortDirection: 'Ascending' } as HeroSort,
    } = {},
  ) {
    const fixture = TestBed.createComponent(HeroTable);
    fixture.componentRef.setInput('heroes', heroes);
    fixture.componentRef.setInput('canDelete', canDelete);
    fixture.componentRef.setInput('sort', sort);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const headers = () => Array.from(element.querySelectorAll('thead th'));
    const header = (name: string) => headers().find((th) => th.textContent?.includes(name))!;
    return { fixture, element, headers, header };
  }

  it('has a caption and column headers in order', async () => {
    const { element, headers } = await render();

    expect(element.querySelector('caption')?.textContent?.trim()).toBe('Heroes');
    expect(headers().map((th) => th.textContent?.replace(/[▲▼]/g, '').trim())).toEqual([
      'Hero name',
      'Real name',
      'Superpowers',
      'Power',
      'Created',
      'Actions',
    ]);
    expect(headers().every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });

  it('renders one row per hero with its name and real name', async () => {
    const { element } = await render();

    const rows = element.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('Superman');
    expect(rows[0].textContent).toContain('Clark Kent');
  });

  it('shows the first two superpowers, then a +N chip naming all of them', async () => {
    const { element } = await render();

    const row = element.querySelector('tbody tr')!;
    const chips = Array.from(row.querySelectorAll('[data-chip]')).map((c) => c.textContent?.trim());
    expect(chips).toEqual(['Flight', 'Super strength', '+2']);
    const more = row.querySelector('[data-chip="more"]')!;
    expect(more.getAttribute('title')).toBe('Flight, Super strength, Heat vision, X-ray vision');
    expect(more.getAttribute('aria-label')).toBe(
      'All superpowers: Flight, Super strength, Heat vision, X-ray vision',
    );
  });

  it('has no +N chip with two superpowers or fewer', async () => {
    const { element } = await render([hero({ superpowers: ['Flight', 'Speed'] })]);

    expect(element.querySelector('[data-chip="more"]')).toBeNull();
  });

  it('shows the power level as an accessible meter', async () => {
    const { element } = await render();

    const meter = element.querySelector('tbody tr [role="meter"]')!;
    expect(meter.getAttribute('aria-valuenow')).toBe('100');
    expect(meter.getAttribute('aria-valuemin')).toBe('1');
    expect(meter.getAttribute('aria-valuemax')).toBe('100');
    expect(meter.textContent).toContain('100');
  });

  describe('sorting', () => {
    it('marks the sorted column with aria-sort and an arrow', async () => {
      const { header } = await render(undefined, {
        sort: { sortBy: 'PowerLevel', sortDirection: 'Descending' },
      });

      expect(header('Power').getAttribute('aria-sort')).toBe('descending');
      expect(header('Power').textContent).toContain('▼');
      expect(header('Hero name').hasAttribute('aria-sort')).toBe(false);
    });

    it('a sortable header is a button; clicking another column sorts it ascending', async () => {
      const { fixture, header } = await render();
      const emitted: HeroSort[] = [];
      fixture.componentInstance.sortChange.subscribe((s) => emitted.push(s));

      header('Power').querySelector('button')!.click();

      expect(emitted).toEqual([{ sortBy: 'PowerLevel', sortDirection: 'Ascending' }]);
    });

    it('clicking the sorted column toggles its direction', async () => {
      const { fixture, header } = await render();
      const emitted: HeroSort[] = [];
      fixture.componentInstance.sortChange.subscribe((s) => emitted.push(s));

      header('Hero name').querySelector('button')!.click();

      expect(emitted).toEqual([{ sortBy: 'HeroName', sortDirection: 'Descending' }]);
    });

    it('Real name and Superpowers are not sortable', async () => {
      const { header } = await render();

      expect(header('Real name').querySelector('button')).toBeNull();
      expect(header('Superpowers').querySelector('button')).toBeNull();
      expect(header('Created').querySelector('button')).not.toBeNull();
    });
  });

  describe('actions', () => {
    it('View and Edit link to the hero, named for screen readers and with a tooltip', async () => {
      const { element } = await render();

      const view = element.querySelector<HTMLAnchorElement>(
        '[data-testid="hero-list-view-button-7"]',
      )!;
      const edit = element.querySelector<HTMLAnchorElement>(
        '[data-testid="hero-list-edit-button-7"]',
      )!;
      expect(view.getAttribute('href')).toBe('/heroes/7');
      expect(view.getAttribute('aria-label')).toBe('View Superman');
      expect(view.getAttribute('title')).toBe('View Superman');
      expect(edit.getAttribute('href')).toBe('/heroes/7/edit');
      expect(edit.getAttribute('aria-label')).toBe('Edit Superman');
    });

    it('hides Delete from regular users', async () => {
      const { element } = await render();

      expect(element.querySelector('[data-testid^="hero-list-delete-button"]')).toBeNull();
    });

    it('shows Delete to admins; clicking it asks the owner to delete that hero', async () => {
      const { fixture, element } = await render(undefined, { canDelete: true });
      const requested: Hero[] = [];
      fixture.componentInstance.deleteRequested.subscribe((h) => requested.push(h));

      const button = element.querySelector<HTMLButtonElement>(
        '[data-testid="hero-list-delete-button-8"]',
      )!;
      expect(button.getAttribute('aria-label')).toBe('Delete Spider-Man');
      button.click();

      expect(requested.map((h) => h.heroName)).toEqual(['Spider-Man']);
    });
  });
});
