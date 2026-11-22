import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthResponse, User } from './auth-models';
import { AUTH_CHANNEL_NAME, AuthService, REFRESH_RACE_RETRY_MS } from './auth-service';

/** In-memory BroadcastChannel: every instance with the same name is one "tab". */
class FakeBroadcastChannel {
  static readonly instances: FakeBroadcastChannel[] = [];
  readonly posted: unknown[] = [];
  onmessage: ((event: MessageEvent) => void) | null = null;
  closed = false;

  constructor(readonly name: string) {
    FakeBroadcastChannel.instances.push(this);
  }

  postMessage(message: unknown): void {
    this.posted.push(message);
  }

  close(): void {
    this.closed = true;
  }

  /** A message arriving from another tab. */
  receive(data: unknown): void {
    this.onmessage?.({ data } as MessageEvent);
  }
}

function user(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    userName: 'Admin',
    email: 'admin@heroregistry.com',
    isAdmin: true,
    createdAt: '2026-11-22T15:00:00Z',
    ...overrides,
  };
}

function authResponse(overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    accessToken: 'access-1',
    expiresAt: '2026-11-22T15:15:00Z',
    tokenType: 'Bearer',
    user: user(),
    ...overrides,
  };
}

function problem(status: number, code: string) {
  return { title: 'Unauthorized', status, code };
}

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    FakeBroadcastChannel.instances.length = 0;
    vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  function channel(): FakeBroadcastChannel {
    return FakeBroadcastChannel.instances[0];
  }

  async function signIn(response = authResponse()): Promise<void> {
    const login = auth.login('Admin', 'Admin');
    http.expectOne('/api/auth/login').flush(response);
    await login;
  }

  describe('initial state', () => {
    it('starts signed out', () => {
      expect(auth.accessToken()).toBeNull();
      expect(auth.currentUser()).toBeNull();
      expect(auth.isAuthenticated()).toBe(false);
      expect(auth.isAdmin()).toBe(false);
    });
  });

  describe('login', () => {
    it('posts the credentials, without credentials mode, to the same-origin API', async () => {
      const login = auth.login('Admin', 'Admin');

      const req = http.expectOne('/api/auth/login');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ userName: 'Admin', password: 'Admin' });
      expect(req.request.withCredentials).toBe(false);
      req.flush(authResponse());
      await login;
    });

    it('stores the token in memory and updates currentUser, isAuthenticated and isAdmin', async () => {
      await signIn();

      expect(auth.accessToken()).toBe('access-1');
      expect(auth.currentUser()?.userName).toBe('Admin');
      expect(auth.isAuthenticated()).toBe(true);
      expect(auth.isAdmin()).toBe(true);
    });

    it('a regular user is not an admin', async () => {
      await signIn(authResponse({ user: user({ isAdmin: false }) }));

      expect(auth.isAdmin()).toBe(false);
    });

    it('never writes to localStorage or sessionStorage', async () => {
      const setItem = vi.spyOn(Storage.prototype, 'setItem');

      await signIn();
      const refresh = firstValueFrom(auth.refresh());
      http.expectOne('/api/auth/refresh').flush(authResponse({ accessToken: 'access-2' }));
      await refresh;

      expect(setItem).not.toHaveBeenCalled();
    });

    it('rejects with the error and stays signed out when the credentials are wrong', async () => {
      const login = auth.login('Admin', 'wrong');
      http.expectOne('/api/auth/login').flush(problem(401, 'auth.invalid_credentials'), {
        status: 401,
        statusText: 'Unauthorized',
      });

      await expect(login).rejects.toMatchObject({ status: 401 });
      expect(auth.isAuthenticated()).toBe(false);
    });
  });

  describe('register', () => {
    it('creates the account and then signs in with it', async () => {
      const register = auth.register({
        userName: 'peter',
        email: 'peter@dailybugle.com',
        password: 'Sp1der-Sense!',
      });

      const created = http.expectOne('/api/auth/register');
      expect(created.request.body).toEqual({
        userName: 'peter',
        email: 'peter@dailybugle.com',
        password: 'Sp1der-Sense!',
      });
      created.flush(user({ id: 2, userName: 'peter', isAdmin: false }), {
        status: 201,
        statusText: 'Created',
      });
      await Promise.resolve();
      const login = http.expectOne('/api/auth/login');
      expect(login.request.body).toEqual({ userName: 'peter', password: 'Sp1der-Sense!' });
      login.flush(authResponse({ user: user({ id: 2, userName: 'peter', isAdmin: false }) }));

      expect((await register).userName).toBe('peter');
      expect(auth.isAuthenticated()).toBe(true);
    });
  });

  describe('logout', () => {
    it('calls the API, clears the session, tells other tabs and goes to the login page', async () => {
      await signIn();

      const logout = auth.logout();
      const req = http.expectOne('/api/auth/logout');
      expect(req.request.method).toBe('POST');
      req.flush(null, { status: 204, statusText: 'No Content' });
      await logout;

      expect(auth.accessToken()).toBeNull();
      expect(auth.currentUser()).toBeNull();
      expect(channel().posted).toEqual(['logout']);
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('still signs out locally when the API cannot be reached', async () => {
      await signIn();

      const logout = auth.logout();
      http.expectOne('/api/auth/logout').error(new ProgressEvent('error'), { status: 0 });
      await logout;

      expect(auth.isAuthenticated()).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('clearSession', () => {
    it('resets the session without calling the API', async () => {
      await signIn();

      auth.clearSession();

      expect(auth.isAuthenticated()).toBe(false);
      expect(auth.currentUser()).toBeNull();
      http.expectNone('/api/auth/logout');
    });
  });

  describe('restoreSession', () => {
    it('restores the user with one refresh at startup (F5)', async () => {
      const restore = auth.restoreSession();
      http.expectOne('/api/auth/refresh').flush(authResponse({ accessToken: 'restored' }));
      await restore;

      expect(auth.accessToken()).toBe('restored');
      expect(auth.currentUser()?.userName).toBe('Admin');
    });

    it('leaves the app signed out, without throwing, when there is no valid session', async () => {
      const restore = auth.restoreSession();
      http
        .expectOne('/api/auth/refresh')
        .flush(problem(401, 'auth.refresh_invalid'), { status: 401, statusText: 'Unauthorized' });

      await expect(restore).resolves.toBeUndefined();
      expect(auth.isAuthenticated()).toBe(false);
    });
  });

  describe('refresh', () => {
    it('replaces the access token and the user', async () => {
      await signIn();

      const refresh = firstValueFrom(auth.refresh());
      http.expectOne('/api/auth/refresh').flush(authResponse({ accessToken: 'access-2' }));

      expect(await refresh).toBe('access-2');
      expect(auth.accessToken()).toBe('access-2');
    });

    it('shares one request between parallel callers', async () => {
      const first = firstValueFrom(auth.refresh());
      const second = firstValueFrom(auth.refresh());

      http.expectOne('/api/auth/refresh').flush(authResponse({ accessToken: 'shared' }));

      expect(await first).toBe('shared');
      expect(await second).toBe('shared');
    });

    it('starts a new request once the previous refresh has finished', async () => {
      const first = firstValueFrom(auth.refresh());
      http.expectOne('/api/auth/refresh').flush(authResponse({ accessToken: 'one' }));
      await first;

      const second = firstValueFrom(auth.refresh());
      http.expectOne('/api/auth/refresh').flush(authResponse({ accessToken: 'two' }));

      expect(await second).toBe('two');
    });

    it('retries once, after a short delay, when another tab won the refresh race', async () => {
      vi.useFakeTimers();
      const refresh = firstValueFrom(auth.refresh());
      http
        .expectOne('/api/auth/refresh')
        .flush(problem(401, 'auth.refresh_race'), { status: 401, statusText: 'Unauthorized' });

      vi.advanceTimersByTime(REFRESH_RACE_RETRY_MS - 1);
      http.expectNone('/api/auth/refresh');
      vi.advanceTimersByTime(1);
      http.expectOne('/api/auth/refresh').flush(authResponse({ accessToken: 'after-race' }));

      expect(await refresh).toBe('after-race');
    });

    it('gives up after a second race', async () => {
      vi.useFakeTimers();
      const refresh = firstValueFrom(auth.refresh());
      http
        .expectOne('/api/auth/refresh')
        .flush(problem(401, 'auth.refresh_race'), { status: 401, statusText: 'Unauthorized' });
      vi.advanceTimersByTime(REFRESH_RACE_RETRY_MS);
      http
        .expectOne('/api/auth/refresh')
        .flush(problem(401, 'auth.refresh_race'), { status: 401, statusText: 'Unauthorized' });

      await expect(refresh).rejects.toMatchObject({ status: 401 });
      vi.advanceTimersByTime(REFRESH_RACE_RETRY_MS * 10);
      http.expectNone('/api/auth/refresh');
    });

    it('does not retry an invalid session', async () => {
      const refresh = firstValueFrom(auth.refresh());
      http
        .expectOne('/api/auth/refresh')
        .flush(problem(401, 'auth.refresh_invalid'), { status: 401, statusText: 'Unauthorized' });

      await expect(refresh).rejects.toMatchObject({ status: 401 });
      http.expectNone('/api/auth/refresh');
    });
  });

  describe('multi-tab', () => {
    it('listens on the hero-registry-auth channel', () => {
      expect(channel().name).toBe(AUTH_CHANNEL_NAME);
      expect(AUTH_CHANNEL_NAME).toBe('hero-registry-auth');
    });

    it('a "logout" message from another tab clears the session and goes to the login page', async () => {
      await signIn();

      channel().receive('logout');

      expect(auth.isAuthenticated()).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
      http.expectNone('/api/auth/logout'); // the other tab already revoked the session
    });

    it('in a hidden tab, clears the session at once but navigates only when the tab is shown again', async () => {
      await signIn();
      const hidden = vi.spyOn(document, 'hidden', 'get').mockReturnValue(true);

      channel().receive('logout');

      expect(auth.isAuthenticated()).toBe(false);
      expect(router.navigate).not.toHaveBeenCalled(); // a hidden document cannot run the view transition

      hidden.mockReturnValue(false);
      document.dispatchEvent(new Event('visibilitychange'));

      expect(router.navigate).toHaveBeenCalledExactlyOnceWith(['/login']);
    });

    it('ignores other messages', async () => {
      await signIn();

      channel().receive({ accessToken: 'injected' });

      expect(auth.accessToken()).toBe('access-1');
    });

    it('never sends a token on the channel', async () => {
      await signIn();
      const logout = auth.logout();
      http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
      await logout;

      expect(JSON.stringify(channel().posted)).not.toContain('access-1');
    });

    it('closes the channel when the application is destroyed', () => {
      const opened = channel();

      TestBed.resetTestingModule();

      expect(opened.closed).toBe(true);
    });
  });
});
