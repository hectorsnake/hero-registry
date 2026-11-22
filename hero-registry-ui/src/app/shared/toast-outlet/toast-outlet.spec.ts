import { TestBed } from '@angular/core/testing';
import { ToastService } from '../../core/ui/toast-service';
import { ToastOutlet } from './toast-outlet';

describe('ToastOutlet', () => {
  async function render() {
    const fixture = TestBed.createComponent(ToastOutlet);
    await fixture.whenStable();
    return {
      fixture,
      element: fixture.nativeElement as HTMLElement,
      toasts: TestBed.inject(ToastService),
    };
  }

  it('announces toasts politely to screen readers', async () => {
    const { element } = await render();

    expect(element.querySelector('[aria-live="polite"]')).not.toBeNull();
  });

  it('renders one message per toast, with its kind', async () => {
    const { fixture, element, toasts } = await render();

    toasts.show('Hero saved.', 'success');
    toasts.show('No permission.', 'error');
    await fixture.whenStable();

    const items = Array.from(element.querySelectorAll('[role="status"]'));
    expect(items.map((i) => i.textContent?.trim())).toEqual([
      expect.stringContaining('Hero saved.'),
      expect.stringContaining('No permission.'),
    ]);
    expect(items.map((i) => i.getAttribute('data-kind'))).toEqual(['success', 'error']);
  });

  it('the close button dismisses that toast', async () => {
    const { fixture, element, toasts } = await render();
    toasts.show('Hero saved.', 'success');
    await fixture.whenStable();

    element.querySelector<HTMLButtonElement>('button[aria-label="Dismiss notification"]')!.click();
    await fixture.whenStable();

    expect(toasts.toasts()).toEqual([]);
    expect(element.querySelectorAll('[role="status"]')).toHaveLength(0);
  });
});
