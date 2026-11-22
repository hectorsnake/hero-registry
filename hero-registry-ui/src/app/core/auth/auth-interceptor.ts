import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth-service';
import { isApiRequest, isTokenEndpoint } from './auth-urls';

/**
 * Adds `Authorization: Bearer <token>` to this API's requests while signed in. Never to other origins,
 * and never to the token endpoints, which authenticate with credentials or the refresh cookie.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).accessToken();

  if (!token || !isApiRequest(req.url) || isTokenEndpoint(req.url)) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
