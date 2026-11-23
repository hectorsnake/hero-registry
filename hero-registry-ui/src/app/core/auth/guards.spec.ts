import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { AuthService } from './auth-service';
import { adminGuard, authGuard, guestGuard } from './guards';

describe('guards', () => {
  const isAuthenticated = signal(false);
  const isAdmin = signal(false);

  beforeEach(() => {
    isAuthenticated.set(false);
    isAdmin.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: isAuthenticated.asReadonly(),
            isAdmin: isAdmin.asReadonly(),
          },
        },
      ],
    });
  });

  function run(guard: typeof authGuard, url: string): boolean | UrlTree {
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, state)) as
      boolean | UrlTree;
  }

  function serialized(result: boolean | UrlTree): string {
    return result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : String(result);
  }

  describe('authGuard', () => {
    it('lets a signed-in user in', () => {
      isAuthenticated.set(true);

      expect(run(authGuard, '/heroes')).toBe(true);
    });

    it('sends a signed-out user to login, remembering where they wanted to go', () => {
      expect(serialized(run(authGuard, '/heroes/7?tab=powers'))).toBe(
        '/login?returnUrl=%2Fheroes%2F7%3Ftab%3Dpowers',
      );
    });
  });

  describe('adminGuard', () => {
    it('lets an administrator in', () => {
      isAuthenticated.set(true);
      isAdmin.set(true);

      expect(run(adminGuard, '/users')).toBe(true);
    });

    it('sends a regular user to the heroes list', () => {
      isAuthenticated.set(true);

      expect(serialized(run(adminGuard, '/users'))).toBe('/heroes');
    });

    it('sends a signed-out visitor to login with the returnUrl', () => {
      expect(serialized(run(adminGuard, '/users'))).toBe('/login?returnUrl=%2Fusers');
    });
  });

  describe('guestGuard (login and register pages)', () => {
    it('lets a signed-out visitor see the page', () => {
      expect(run(guestGuard, '/login')).toBe(true);
    });

    it('sends a signed-in user to the heroes list', () => {
      isAuthenticated.set(true);

      expect(serialized(run(guestGuard, '/register'))).toBe('/heroes');
    });
  });
});
