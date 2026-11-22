import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from './auth-service';

/** Signed-in users only. Others go to login and come back to `returnUrl` afterwards. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isAuthenticated() || toLogin(state.url);

/** Administrators only. Regular users go to the heroes list; signed-out visitors to login. */
export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);

  if (!auth.isAuthenticated()) {
    return toLogin(state.url);
  }

  return auth.isAdmin() || inject(Router).createUrlTree(['/heroes']);
};

function toLogin(returnUrl: string): UrlTree {
  return inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl } });
}
