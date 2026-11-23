import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth-service';
import { isApiRequest, isTokenEndpoint } from './auth-urls';

/** Marks a request that was already retried after a refresh, so it is never retried twice. */
export const RETRIED = new HttpContextToken<boolean>(() => false);

/**
 * Silent session renewal (AC12): a 401 from the API means the 15-minute access token expired. Refresh it
 * with the HttpOnly cookie (one shared request for parallel 401s) and retry the request once. Only when
 * the refresh itself fails is the user sent to the login page, with a `returnUrl` to come back to
 * and `reason=expired`, so the page can explain why.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

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
          // The login page shows "Your session has expired" for reason=expired (T13).
          void router.navigate(['/login'], {
            queryParams: { returnUrl: router.url, reason: 'expired' },
          });
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
