import { HttpClient } from '@angular/common/http';
import { DOCUMENT, DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  Observable,
  finalize,
  firstValueFrom,
  map,
  retry,
  shareReplay,
  tap,
  throwError,
  timer,
} from 'rxjs';
import { problemCode } from '../api/problem-details';
import { AuthResponse, RegisterRequest, User } from './auth-models';
import { AUTH_URLS } from './auth-urls';

/** The channel that tells the other tabs of this browser about a logout. Never carries a token. */
export const AUTH_CHANNEL_NAME = 'hero-registry-auth';

/** Wait before retrying a refresh that lost the race to another tab (`auth.refresh_race`, T09.1). */
export const REFRESH_RACE_RETRY_MS = 200;

const LOGOUT_MESSAGE = 'logout';

/**
 * The session, in memory only (AC12). The access token is a signal and is never written to
 * localStorage, sessionStorage or a readable cookie; the refresh token is an HttpOnly cookie that only
 * the API and the browser handle, sent automatically to `/api/auth` (same origin, SameSite=Strict).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly #http = inject(HttpClient);
  readonly #router = inject(Router);
  readonly #document = inject(DOCUMENT);

  readonly #accessToken = signal<string | null>(null);
  readonly #user = signal<User | null>(null);
  #refreshInFlight: Observable<string> | null = null;
  readonly #channel = openChannel();

  readonly accessToken = this.#accessToken.asReadonly();
  readonly currentUser = this.#user.asReadonly();
  readonly isAuthenticated = computed(() => this.#accessToken() !== null);
  readonly isAdmin = computed(() => this.#user()?.isAdmin === true);

  constructor() {
    if (this.#channel) {
      const channel = this.#channel;
      channel.onmessage = (event: MessageEvent) => {
        if (event.data === LOGOUT_MESSAGE) {
          this.clearSession();
          this.#goToLoginWhenVisible();
        }
      };
      inject(DestroyRef).onDestroy(() => channel.close());
    }
  }

  /** Signs in. The browser stores the refresh cookie from the response by itself. */
  async login(userName: string, password: string): Promise<User> {
    const response = await firstValueFrom(
      this.#http.post<AuthResponse>(AUTH_URLS.login, { userName, password }),
    );
    this.#startSession(response);
    return response.user;
  }

  /** Creates a regular account, then signs in with it. */
  async register(request: RegisterRequest): Promise<User> {
    await firstValueFrom(this.#http.post<User>(AUTH_URLS.register, request));
    return this.login(request.userName, request.password);
  }

  /**
   * A new access token from the refresh cookie. Parallel callers (several 401s at once) share one
   * request. A lost race with another tab (`auth.refresh_race`) is retried once, after a short delay.
   */
  refresh(): Observable<string> {
    this.#refreshInFlight ??= this.#http.post<AuthResponse>(AUTH_URLS.refresh, null).pipe(
      retry({
        count: 1,
        delay: (error: unknown) =>
          problemCode(error) === 'auth.refresh_race'
            ? timer(REFRESH_RACE_RETRY_MS)
            : throwError(() => error),
      }),
      tap((response) => this.#startSession(response)),
      map((response) => response.accessToken),
      finalize(() => (this.#refreshInFlight = null)),
      shareReplay(1),
    );
    return this.#refreshInFlight;
  }

  /** Restores the session after a page load (F5). No valid session simply means "signed out". */
  async restoreSession(): Promise<void> {
    try {
      await firstValueFrom(this.refresh());
    } catch {
      this.clearSession();
    }
  }

  /** Revokes the session on the server, signs out here and in the other tabs, then shows the login page. */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.#http.post<void>(AUTH_URLS.logout, null));
    } catch {
      // Signing out locally must always work, even offline. The cookie expires on its own.
    }

    this.#channel?.postMessage(LOGOUT_MESSAGE);
    this.#endSession();
  }

  /** Forgets the session in this tab, without calling the API. */
  clearSession(): void {
    this.#accessToken.set(null);
    this.#user.set(null);
  }

  #startSession(response: AuthResponse): void {
    this.#accessToken.set(response.accessToken);
    this.#user.set(response.user);
  }

  #endSession(): void {
    this.clearSession();
    void this.#router.navigate(['/login']);
  }

  /**
   * A hidden tab cannot run the router's view transition (the browser aborts it), so a logout from
   * another tab navigates once this tab is shown again. The session is already cleared by then.
   */
  #goToLoginWhenVisible(): void {
    if (!this.#document.hidden) {
      void this.#router.navigate(['/login']);
      return;
    }

    this.#document.addEventListener(
      'visibilitychange',
      () => void this.#router.navigate(['/login']),
      { once: true },
    );
  }
}

function openChannel(): BroadcastChannel | null {
  return typeof BroadcastChannel === 'function' ? new BroadcastChannel(AUTH_CHANNEL_NAME) : null;
}
