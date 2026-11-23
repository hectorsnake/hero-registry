import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Paginator } from './paginator';

@Component({
  imports: [Paginator],
  template: `<app-paginator [(page)]="page" [totalPages]="totalPages()" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly page = signal(1);
  readonly totalPages = signal(3);
}

describe('Paginator', () => {
  async function render(page = 1, totalPages = 3) {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.page.set(page);
    fixture.componentInstance.totalPages.set(totalPages);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const button = (label: string) =>
      Array.from(element.querySelectorAll('button')).find(
        (b) => b.getAttribute('aria-label') === label,
      )!;
    return { fixture, host: fixture.componentInstance, element, button };
  }

  it('shows "Page X of Y" inside a labelled navigation landmark', async () => {
    const { element } = await render(2, 5);

    expect(element.querySelector('nav[aria-label="Pagination"]')).not.toBeNull();
    expect(element.textContent).toContain('Page 2 of 5');
  });

  it('disables Previous on the first page and Next on the last', async () => {
    const first = await render(1, 3);
    expect(first.button('Previous page').disabled).toBe(true);
    expect(first.button('Next page').disabled).toBe(false);

    first.host.page.set(3);
    await first.fixture.whenStable();
    expect(first.button('Previous page').disabled).toBe(false);
    expect(first.button('Next page').disabled).toBe(true);
  });

  it('Next and Previous change the page (two-way binding)', async () => {
    const view = await render(2, 3);

    view.button('Next page').click();
    expect(view.host.page()).toBe(3);

    view.button('Previous page').click();
    expect(view.host.page()).toBe(2);
  });

  it('renders nothing when everything fits on one page', async () => {
    const { element } = await render(1, 1);

    expect(element.querySelector('nav')).toBeNull();
  });
});
