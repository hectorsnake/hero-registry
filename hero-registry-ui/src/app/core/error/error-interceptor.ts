import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { problemTitle } from '../api/problem-details';
import { ToastService } from '../ui/toast-service';

export const FORBIDDEN_MESSAGE = "You don't have permission to do that.";
export const OFFLINE_MESSAGE = "Can't reach the server. Check your connection and try again.";
export const SERVER_ERROR_MESSAGE = 'Something went wrong. Please try again.';

/**
 * App-wide feedback for errors no screen can fix: 403, network failures and 5xx. 400 and 409 belong
 * to the forms (field errors), 401 to the refresh interceptor and 404 to the page. The error is always
 * re-thrown, so the caller still knows the request failed.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toasts = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const message = messageFor(error);
        if (message) {
          toasts.show(message, 'error');
        }
      }

      return throwError(() => error);
    }),
  );
};

function messageFor(error: HttpErrorResponse): string | null {
  if (error.status === 403) {
    return FORBIDDEN_MESSAGE;
  }

  if (error.status === 0) {
    return OFFLINE_MESSAGE;
  }

  // Only the problem title (written for users, T11), never the raw body of a proxy or crash page.
  return error.status >= 500 ? problemTitle(error, SERVER_ERROR_MESSAGE) : null;
}
