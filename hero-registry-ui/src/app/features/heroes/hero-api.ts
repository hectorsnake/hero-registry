import { HttpClient, HttpResourceRef, httpResource } from '@angular/common/http';
import { Injectable, Signal, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import { Hero, HeroQuery, HeroRequest } from './hero-models';

const HEROES_URL = '/api/heroes';

/**
 * Hero writes (T10). Reads are the resource factories below, so a page refetches by itself when its
 * signal parameters change; after a write, call the resource's `reload()`.
 */
@Injectable({ providedIn: 'root' })
export class HeroApi {
  readonly #http = inject(HttpClient);

  create(request: HeroRequest): Observable<Hero> {
    return this.#http.post<Hero>(HEROES_URL, request);
  }

  update(id: number, request: HeroRequest): Observable<Hero> {
    return this.#http.put<Hero>(`${HEROES_URL}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.#http.delete<void>(`${HEROES_URL}/${id}`);
  }
}

/** One page of heroes; refetches (and cancels the previous request) whenever `query` changes. */
export function heroesResource(
  query: Signal<HeroQuery>,
): HttpResourceRef<PagedResult<Hero> | undefined> {
  return httpResource<PagedResult<Hero>>(() => ({ url: HEROES_URL, params: toParams(query()) }));
}

/** One hero; no request while `id` is undefined (create mode, or before the route input is set). */
export function heroResource(id: Signal<number | undefined>): HttpResourceRef<Hero | undefined> {
  return httpResource<Hero>(() => {
    const heroId = id();
    return heroId === undefined ? undefined : `${HEROES_URL}/${heroId}`;
  });
}

function toParams(query: HeroQuery): Record<string, string | number> {
  const params: Record<string, string | number> = {
    page: query.page,
    pageSize: query.pageSize,
    sortBy: query.sortBy,
    sortDirection: query.sortDirection,
  };

  const search = query.search?.trim();
  if (search) {
    params['search'] = search;
  }

  return params;
}
