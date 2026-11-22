import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { User } from '../../core/auth/auth-models';
import { AuthService } from '../../core/auth/auth-service';
import { ViewportService } from '../../core/ui/viewport-service';
import { Navbar } from './navbar';

describe('Navbar', () => {
  const currentUser = signal<User | null>(null);
  const isMobile = signal(false);
  const auth = {
    currentUser: currentUser.asReadonly(),
    isAuthenticated: computed(() => currentUser() !== null),
    isAdmin: computed(() => currentUser()?.isAdmin === true),
    logout: vi.fn(() => Promise.resolve()),
  };

  function user(overrides: Partial<User> = {}): User {
    return {
      id: 2,
      userName: 'peter',
      email: 'peter@dailybugle.com',
      isAdmin: false,
      createdAt: '2026-11-22T15:00:00Z',
      ...overrides,
    };
  }

  beforeEach(() => {
    currentUser.set(user());
    isMobile.set(false);
    auth.logout.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: ViewportService, useValue: { isMobile: isMobile.asReadonly() } },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  async function render() {
    const fixture = TestBed.createComponent(Navbar);
    fixture.componentRef.setInput('appName', 'SuperHero Registry');
    // Let the profile httpResource issue its request; awaiting stability first would wait for it forever.
    TestBed.tick();
    TestBed.inject(HttpTestingController)
      .match('/api/auth/me')
      .forEach((req) => req.flush(currentUser()));
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const button = (name: string) =>
      Array.from(element.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === name || b.getAttribute('aria-label') === name,
      );
    return { fixture, element, button };
  }

  it('shows the app name and the signed-in user name', async () => {
    const { element } = await render();

    expect(element.textContent).toContain('SuperHero Registry');
    expect(element.textContent).toContain('peter');
  });

  it('shows no admin badge for a regular user', async () => {
    const { element } = await render();

    expect(element.textContent).not.toContain('Admin');
  });

  it('shows the admin badge only for an administrator', async () => {
    currentUser.set(user({ userName: 'Admin', isAdmin: true }));

    const { element } = await render();

    expect(element.querySelector('[aria-label="Administrator"]')?.textContent?.trim()).toBe(
      'Admin',
    );
  });

  it('the logout button calls AuthService.logout()', async () => {
    const { button } = await render();

    button('Log out')!.click();

    expect(auth.logout).toHaveBeenCalledTimes(1);
  });

  it('shows the email from GET /api/auth/me in the profile', async () => {
    const { element } = await render();

    expect(element.textContent).toContain('peter@dailybugle.com');
  });

  describe('on mobile', () => {
    beforeEach(() => isMobile.set(true));

    it('collapses the menu behind a toggle button', async () => {
      const { element, button } = await render();

      const toggle = button('Menu')!;
      expect(toggle.getAttribute('aria-expanded')).toBe('false');
      expect(element.querySelector('#navbar-menu')?.hasAttribute('hidden')).toBe(true);
    });

    it('the toggle opens and closes the menu', async () => {
      const { fixture, element, button } = await render();

      button('Menu')!.click();
      await fixture.whenStable();
      expect(button('Menu')!.getAttribute('aria-expanded')).toBe('true');
      expect(element.querySelector('#navbar-menu')?.hasAttribute('hidden')).toBe(false);

      button('Menu')!.click();
      await fixture.whenStable();
      expect(element.querySelector('#navbar-menu')?.hasAttribute('hidden')).toBe(true);
    });
  });

  it('on a desktop the menu is always visible and there is no toggle', async () => {
    const { element, button } = await render();

    expect(button('Menu')).toBeUndefined();
    expect(element.querySelector('#navbar-menu')?.hasAttribute('hidden')).toBe(false);
  });
});
