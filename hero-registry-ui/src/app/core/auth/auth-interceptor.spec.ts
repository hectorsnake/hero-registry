import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './auth-interceptor';
import { AuthService } from './auth-service';

describe('authInterceptor', () => {
  const token = signal<string | null>('access-1');
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    token.set('access-1');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { accessToken: token.asReadonly() } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  function authorizationOf(url: string): string | null {
    void firstValueFrom(http.get(url)).catch(() => undefined);
    const req = backend.expectOne(url);
    req.flush({});
    return req.request.headers.get('Authorization');
  }

  it('adds the bearer token to API requests', () => {
    expect(authorizationOf('/api/heroes')).toBe('Bearer access-1');
  });

  it('uses the current token, read from the signal on every request', () => {
    token.set('access-2');

    expect(authorizationOf('/api/auth/me')).toBe('Bearer access-2');
  });

  it('sends no header when signed out', () => {
    token.set(null);

    expect(authorizationOf('/api/heroes')).toBeNull();
  });

  it('never sends the token outside the API', () => {
    expect(authorizationOf('https://cdn.example.com/lib.js')).toBeNull();
    expect(authorizationOf('/assets/i18n/en.json')).toBeNull();
  });

  it.each(['/api/auth/login', '/api/auth/register', '/api/auth/refresh', '/api/auth/logout'])(
    'never sends the token to the token endpoint %s',
    (url) => {
      void firstValueFrom(http.post(url, null)).catch(() => undefined);
      const req = backend.expectOne(url);
      req.flush({});

      expect(req.request.headers.has('Authorization')).toBe(false);
    },
  );
});
