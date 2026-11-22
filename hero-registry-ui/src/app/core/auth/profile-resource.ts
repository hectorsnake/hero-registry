import { HttpResourceRef, httpResource } from '@angular/common/http';
import { Signal } from '@angular/core';
import { User } from './auth-models';
import { AUTH_URLS } from './auth-urls';

/**
 * The signed-in user's profile from `GET /api/auth/me`: the reference `httpResource` read (see
 * `core/api/README.md`). No request while signed out; it reloads by itself when `signedIn` changes.
 * Call it from an injection context, such as a component field initializer.
 */
export function profileResource(signedIn: Signal<boolean>): HttpResourceRef<User | undefined> {
  return httpResource<User>(() => (signedIn() ? AUTH_URLS.me : undefined));
}
