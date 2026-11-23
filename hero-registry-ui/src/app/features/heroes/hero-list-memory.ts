import { Injectable, signal } from '@angular/core';
import { Params } from '@angular/router';

/**
 * The heroes list's last query (search, page, sort), so the detail and form pages can link back to
 * exactly the list the user came from. Kept in memory: a fresh tab simply goes back to page 1.
 */
@Injectable({ providedIn: 'root' })
export class HeroListMemory {
  readonly #queryParams = signal<Params>({});

  readonly queryParams = this.#queryParams.asReadonly();

  remember(queryParams: Params): void {
    this.#queryParams.set(queryParams);
  }
}
