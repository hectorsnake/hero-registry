# core/api: talking to the API

The API is **same-origin**: the UI calls relative `/api/...` URLs. `ng serve` proxies them to the API under Aspire (`proxy.conf.mjs`), and nginx does the same under Docker Compose. So:

- No base-URL token and no hard-coded hosts in features.
- No `withCredentials` and no CORS credentials. The browser sends the `hr_refresh` cookie (`HttpOnly`, `SameSite=Strict`, `Path=/api/auth`) by itself, and only to the auth endpoints.
- The bearer token is added by `authInterceptor`. Features never set it themselves.

## Reads: `httpResource` with signal parameters

```ts
// heroes-api.ts (T14)
export function heroesResource(query: Signal<HeroQuery>) {
  return httpResource<PagedResult<Hero>>(() => ({ url: '/api/heroes', params: toParams(query()) }));
}

// hero-list.ts
readonly search = signal('');
readonly page = linkedSignal({ source: this.search, computation: () => 1 }); // back to page 1 on a new search
protected readonly heroes = heroesResource(computed(() => ({ search: this.search(), page: this.page() })));
```

- Changing `search()` or `page()` refetches automatically and cancels the request still in flight.
- Return `undefined` from the request function to send no request (see `profileResource`, which loads `GET /api/auth/me` only while signed in and is shown in the navbar).
- Templates use `isLoading()`, `error()`, `hasValue()` / `value()` and `reload()`. Nothing is copied into other signals with `effect()` or `subscribe()`.

## Writes: `HttpClient`, then `reload()`

```ts
await firstValueFrom(this.#http.post<Hero>('/api/heroes', request));
this.heroes.reload();
```

## Errors: RFC 9457 problems (T11)

| Status | Who handles it |
| ------ | -------------- |
| 400, 409 | The form. `toFieldErrors(error, fields)` maps `errors` (camelCase, paths such as `superpowers[0]`) onto the form's fields, ignoring keys that aren't fields. |
| 401 | `refreshInterceptor`: one silent refresh and one retry. If the refresh fails, the user goes to `/login?returnUrl=...`. |
| 403, 0, 5xx | `errorInterceptor`: a toast. For a 5xx it shows the problem `title`, never raw server text. |
| 404 | The page (a "not found" state). |
