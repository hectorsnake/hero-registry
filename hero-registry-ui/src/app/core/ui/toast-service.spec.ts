import { TestBed } from '@angular/core/testing';
import { TOAST_DURATION_MS, ToastService } from './toast-service';

describe('ToastService', () => {
  let toasts: ToastService;

  beforeEach(() => {
    vi.useFakeTimers();
    toasts = TestBed.inject(ToastService);
  });

  afterEach(() => vi.useRealTimers());

  it('starts with no toasts', () => {
    expect(toasts.toasts()).toEqual([]);
  });

  it('show adds a toast with its kind and text', () => {
    toasts.show('Hero saved.', 'success');

    expect(toasts.toasts()).toEqual([
      { id: expect.any(Number), kind: 'success', text: 'Hero saved.' },
    ]);
  });

  it('shows information toasts by default', () => {
    toasts.show('Hello');

    expect(toasts.toasts()[0].kind).toBe('info');
  });

  it('dismiss removes only that toast', () => {
    const first = toasts.show('First');
    toasts.show('Second');

    toasts.dismiss(first);

    expect(toasts.toasts().map((t) => t.text)).toEqual(['Second']);
  });

  it('removes a toast automatically after about 5 seconds', () => {
    toasts.show('Bye soon');

    vi.advanceTimersByTime(TOAST_DURATION_MS - 1);
    expect(toasts.toasts()).toHaveLength(1);

    vi.advanceTimersByTime(1);
    expect(toasts.toasts()).toEqual([]);
  });

  it('does not stack the same message twice, for example after parallel 403s', () => {
    const first = toasts.show('No permission', 'error');
    const second = toasts.show('No permission', 'error');

    expect(second).toBe(first);
    expect(toasts.toasts()).toHaveLength(1);
  });

  it('dismissing an unknown or already removed toast does nothing', () => {
    const id = toasts.show('Once');
    toasts.dismiss(id);

    expect(() => toasts.dismiss(id)).not.toThrow();
    expect(toasts.toasts()).toEqual([]);
  });
});
