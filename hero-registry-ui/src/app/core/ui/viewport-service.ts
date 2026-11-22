import { DestroyRef, Injectable, inject, signal } from '@angular/core';

/** The breakpoint below which the layout switches to its mobile form. */
export const MOBILE_QUERY = '(max-width: 768px)';

/** Viewport breakpoints as signals (no `@angular/cdk` needed for one media query). */
@Injectable({ providedIn: 'root' })
export class ViewportService {
  readonly #isMobile = signal(false);

  readonly isMobile = this.#isMobile.asReadonly();

  constructor() {
    if (typeof globalThis.matchMedia !== 'function') {
      return; // server rendering or an old test environment: assume a desktop
    }

    const query = globalThis.matchMedia(MOBILE_QUERY);
    const listener = (event: { matches: boolean }): void => this.#isMobile.set(event.matches);

    this.#isMobile.set(query.matches);
    query.addEventListener('change', listener);
    inject(DestroyRef).onDestroy(() => query.removeEventListener('change', listener));
  }
}
