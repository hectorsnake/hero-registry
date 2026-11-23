import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Observable, Subject, firstValueFrom, throwError } from 'rxjs';
import { ToastService } from '../ui/toast-service';
import { AuthService } from './auth-service';
import { refreshInterceptor } from './refresh-interceptor';

describe('refreshInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;
  let refreshes: Subject<string>;
  const auth = {
    refresh: vi.fn<() => Observable<string>>(),
    clearSession: vi.fn(),
  };
  const toasts = { show: vi.fn() };

  beforeEach(() => {
    refreshes = new Subject<string>();
    auth.refresh.mockReset().mockReturnValue(refreshes);
    auth.clearSession.mockReset();
    toasts.show.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([refreshInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toasts },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(router, 'url', 'get').mockReturnValue('/heroes?page=2');
  });

  afterEach(() => backend.verify());

  function unauthorized(url: string): void {
    backend
      .expectOne(url)
      .flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });
  }

  function newToken(token: string): void {
    refreshes.next(token);
    refreshes.complete();
  }

  it('on a 401, refreshes and retries the original request once with the new token', async () => {
    const result = firstValueFrom(http.get<{ ok: boolean }>('/api/heroes'));
    unauthorized('/api/heroes');

    newToken('access-2');
    const retried = backend.expectOne('/api/heroes');
    expect(retried.request.headers.get('Authorization')).toBe('Bearer access-2');
    retried.flush({ ok: true });

    expect(await result).toEqual({ ok: true });
    expect(auth.refresh).toHaveBeenCalledTimes(1);
  });

  it('two parallel 401s share the refresh and are both retried', async () => {
    const heroes = firstValueFrom(http.get('/api/heroes'));
    const me = firstValueFrom(http.get('/api/auth/me'));
    unauthorized('/api/heroes');
    unauthorized('/api/auth/me');

    newToken('access-2');
    backend.expectOne('/api/heroes').flush(['hero']);
    backend.expectOne('/api/auth/me').flush({ userName: 'Admin' });

    expect(await heroes).toEqual(['hero']);
    expect(await me).toEqual({ userName: 'Admin' });
    // AuthService shares one in-flight request between callers (see its spec), so both get one token.
    expect(new Set(auth.refresh.mock.results.map((r) => r.value)).size).toBe(1);
  });

  it('when the refresh fails, clears the session and sends the user to login with a returnUrl and reason=expired', async () => {
    auth.refresh.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
    const result = firstValueFrom(http.get('/api/heroes'));
    unauthorized('/api/heroes');

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(auth.clearSession).toHaveBeenCalled();
    // The login page explains why (T13), so there is no toast on top of it.
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/heroes?page=2', reason: 'expired' },
    });
    expect(toasts.show).not.toHaveBeenCalled();
  });

  it('a retried request that gets 401 again is not retried a second time', async () => {
    const result = firstValueFrom(http.get('/api/heroes'));
    unauthorized('/api/heroes');
    newToken('access-2');

    unauthorized('/api/heroes');

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(auth.refresh).toHaveBeenCalledTimes(1);
    backend.expectNone('/api/heroes');
  });

  it.each(['/api/auth/login', '/api/auth/refresh', '/api/auth/register', '/api/auth/logout'])(
    'a 401 from the token endpoint %s is passed through without a refresh',
    async (url) => {
      const result = firstValueFrom(http.post(url, null));
      unauthorized(url);

      await expect(result).rejects.toMatchObject({ status: 401 });
      expect(auth.refresh).not.toHaveBeenCalled();
    },
  );

  it('other errors are passed through untouched', async () => {
    const result = firstValueFrom(http.get('/api/heroes/9'));
    backend
      .expectOne('/api/heroes/9')
      .flush({ title: 'Not Found', status: 404 }, { status: 404, statusText: 'Not Found' });

    await expect(result).rejects.toMatchObject({ status: 404 });
    expect(auth.refresh).not.toHaveBeenCalled();
  });

  it('a 401 from outside the API is not handled', async () => {
    const result = firstValueFrom(http.get('https://other.example.com/data'));
    unauthorized('https://other.example.com/data');

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(auth.refresh).not.toHaveBeenCalled();
  });
});
