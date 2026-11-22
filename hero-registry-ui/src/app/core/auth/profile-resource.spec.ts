import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { User } from './auth-models';
import { profileResource } from './profile-resource';

describe('profileResource', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  const admin: User = {
    id: 1,
    userName: 'Admin',
    email: 'admin@heroregistry.com',
    isAdmin: true,
    createdAt: '2026-11-22T15:00:00Z',
  };

  it('loads GET /api/auth/me while signed in', async () => {
    const signedIn = signal(true);
    const profile = TestBed.runInInjectionContext(() => profileResource(signedIn));
    TestBed.tick();

    const req = backend.expectOne('/api/auth/me');
    expect(req.request.method).toBe('GET');
    req.flush(admin);
    await TestBed.inject(ApplicationRef).whenStable();

    expect(profile.value()?.email).toBe('admin@heroregistry.com');
  });

  it('sends no request while signed out, and loads once the user signs in', () => {
    const signedIn = signal(false);
    const profile = TestBed.runInInjectionContext(() => profileResource(signedIn));
    TestBed.tick();

    backend.expectNone('/api/auth/me');
    expect(profile.value()).toBeUndefined();

    signedIn.set(true);
    TestBed.tick();
    backend.expectOne('/api/auth/me').flush(admin);
  });
});
