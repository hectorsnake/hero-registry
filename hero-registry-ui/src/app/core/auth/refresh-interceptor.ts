import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { ToastService } from '../ui/toast-service';
import { AuthService } from './auth-service';
import { isApiRequest, isTokenEndpoint } from './auth-urls';

/** Marks a request that was already retried after a refresh, so it is never retried twice. */
export const RETRIED = new HttpContextToken<boolean>(() => false);

export const SESSION_EXPIRED_MESSAGE = 'Your session has expired. Please sign in again.';

/**
 * Silent session renewal (AC12): a 401 from the API means the 15-minute access token expired. Refresh it
 * with the HttpOnly cookie (one shared request for parallel 401s) and retry the request once. Only when
 * the refresh itself fails is the user sent to the login page, with a `returnUrl` to come back to.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toasts = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      const canRefresh =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        isApiRequest(req.url) &&
        !isTokenEndpoint(req.url) &&
        !req.context.get(RETRIED);

      if (!canRefresh) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError(() => {
          auth.clearSession();
          toasts.show(SESSION_EXPIRED_MESSAGE, 'warning');
          void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
          return throwError(() => error);
        }),
        switchMap((token) =>
          next(
            req.clone({
              setHeaders: { Authorization: `Bearer ${token}` },
              context: req.context.set(RETRIED, true),
            }),
          ),
        ),
      );
    }),
  );
};
