import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Hero } from '../hero-models';
import { hero } from '../../../../testing/heroes';
import { HeroCard } from './hero-card';

describe('HeroCard', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  async function render(canDelete = false) {
    const fixture = TestBed.createComponent(HeroCard);
    fixture.componentRef.setInput('hero', hero());
    fixture.componentRef.setInput('canDelete', canDelete);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows the hero name, real name, superpower chips and power meter', async () => {
    const { element } = await render();

    expect(element.querySelector('h2, h3')?.textContent?.trim()).toBe('Superman');
    expect(element.textContent).toContain('Clark Kent');
    expect(element.querySelectorAll('[data-chip]').length).toBe(3); // 2 chips + "+2"
    expect(element.querySelector('[role="meter"]')?.getAttribute('aria-valuenow')).toBe('100');
  });

  it('offers View and Edit, and Delete only to admins', async () => {
    const regular = await render(false);
    expect(
      regular.element.querySelector('a[aria-label="View Superman"]')?.getAttribute('href'),
    ).toBe('/heroes/7');
    expect(
      regular.element.querySelector('a[aria-label="Edit Superman"]')?.getAttribute('href'),
    ).toBe('/heroes/7/edit');
    expect(regular.element.querySelector('button[aria-label="Delete Superman"]')).toBeNull();
  });

  it('Delete asks the owner to delete the hero', async () => {
    const { fixture, element } = await render(true);
    const requested: Hero[] = [];
    fixture.componentInstance.deleteRequested.subscribe((h) => requested.push(h));

    element.querySelector<HTMLButtonElement>('button[aria-label="Delete Superman"]')!.click();

    expect(requested.map((h) => h.id)).toEqual([7]);
  });
});
