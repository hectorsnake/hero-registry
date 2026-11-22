import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth-interceptor';
import { AuthService } from './core/auth/auth-service';
import { refreshInterceptor } from './core/auth/refresh-interceptor';
import { errorInterceptor } from './core/error/error-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Order matters: attach the token, then refresh-and-retry on 401, then app-wide error toasts.
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor, refreshInterceptor, errorInterceptor]),
    ),
    provideRouter(routes, withComponentInputBinding(), withViewTransitions()),
    // One silent refresh before the first navigation, so guards see a session restored after F5.
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
