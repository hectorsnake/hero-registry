import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../../../core/auth/auth-service';
import { AUTH_MESSAGES } from '../auth-schemas';
import { Login } from './login';

describe('Login', () => {
  const auth = {
    login: vi.fn<(userName: string, password: string) => Promise<unknown>>(),
    isAuthenticated: signal(false),
  };
  let router: Router;

  beforeEach(() => {
    auth.login.mockReset().mockResolvedValue({ id: 1, userName: 'Admin' });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  async function render(inputs: { returnUrl?: string; reason?: string } = {}) {
    const fixture = TestBed.createComponent(Login);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await fixture.whenStable();
    return page(fixture);
  }

  function page(fixture: ComponentFixture<Login>) {
    const element = fixture.nativeElement as HTMLElement;
    const input = (label: string) => {
      const id = Array.from(element.querySelectorAll('label')).find(
        (l) => l.textContent?.trim() === label,
      )?.htmlFor;
      return element.querySelector<HTMLInputElement>(`#${id}`)!;
    };
    const submitButton = () => element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    return {
      fixture,
      element,
      input,
      submitButton,
      async type(label: string, value: string) {
        const field = input(label);
        field.value = value;
        field.dispatchEvent(new Event('input'));
        await fixture.whenStable();
      },
      /** Like pressing Enter in a field: the form's submit event. */
      async submit() {
        element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
        await fixture.whenStable();
      },
      alertText: () => element.querySelector('[aria-live="polite"]')?.textContent?.trim() ?? '',
    };
  }

  async function filledIn(inputs: { returnUrl?: string; reason?: string } = {}) {
    const view = await render(inputs);
    await view.type('User name', 'Admin');
    await view.type('Password', 'Admin');
    return view;
  }

  it('submitting while empty shows every error and focuses the first field, without calling the API', async () => {
    const view = await render();

    await view.submit();

    expect(auth.login).not.toHaveBeenCalled();
    expect(view.input('User name').getAttribute('aria-invalid')).toBe('true');
    expect(view.input('Password').getAttribute('aria-invalid')).toBe('true');
    expect(view.element.textContent).toContain(AUTH_MESSAGES.required);
    expect(document.activeElement).toBe(view.input('User name'));
  });

  it('keeps the button enabled while invalid, so a fast Enter or autofill submit is never lost', async () => {
    const { submitButton } = await render();

    expect(submitButton().disabled).toBe(false);
  });

  it('labels the inputs and sets the autocomplete hints', async () => {
    const { input } = await render();

    expect(input('User name').autocomplete).toBe('username');
    expect(input('Password').autocomplete).toBe('current-password');
    expect(input('Password').type).toBe('password');
  });

  it('submits the credentials to AuthService.login and goes to /heroes', async () => {
    const { submitButton, fixture } = await filledIn();

    submitButton().click();
    await fixture.whenStable();

    expect(auth.login).toHaveBeenCalledExactlyOnceWith('Admin', 'Admin');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/heroes');
  });

  it('Enter submits the form', async () => {
    const view = await filledIn();

    await view.submit();

    expect(auth.login).toHaveBeenCalledTimes(1);
  });

  it('goes back to the returnUrl after signing in', async () => {
    const view = await filledIn({ returnUrl: '/heroes/7?tab=powers' });

    await view.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/heroes/7?tab=powers');
  });

  it.each(['https://evil.com', '//evil.com', '/\\evil.com', 'javascript:alert(1)'])(
    'ignores the external returnUrl %s (no open redirect)',
    async (returnUrl) => {
      const view = await filledIn({ returnUrl });

      await view.submit();

      expect(router.navigateByUrl).toHaveBeenCalledWith('/heroes');
    },
  );

  it('a 401 shows the generic message, without blaming one field', async () => {
    auth.login.mockRejectedValue(
      new HttpErrorResponse({
        status: 401,
        error: { title: 'Unauthorized', status: 401, code: 'auth.invalid_credentials' },
      }),
    );
    const view = await filledIn();

    await view.submit();

    expect(view.alertText()).toBe(AUTH_MESSAGES.invalidCredentials);
    expect(view.input('User name').getAttribute('aria-invalid')).toBe('false');
    expect(view.input('Password').getAttribute('aria-invalid')).toBe('false');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('clears the message on the next attempt', async () => {
    auth.login.mockRejectedValueOnce(new HttpErrorResponse({ status: 401 }));
    const view = await filledIn();
    await view.submit();

    await view.submit();

    expect(view.alertText()).toBe('');
  });

  it('disables the button and shows progress while the request is pending', async () => {
    let finish!: (value: unknown) => void;
    auth.login.mockReturnValue(new Promise((resolve) => (finish = resolve)));
    const view = await filledIn();

    view.submitButton().click();
    await view.fixture.whenStable();

    expect(view.submitButton().disabled).toBe(true);
    expect(view.submitButton().getAttribute('aria-busy')).toBe('true');
    expect(view.submitButton().textContent).toContain('Signing in');

    finish({});
    await vi.waitFor(() => expect(router.navigateByUrl).toHaveBeenCalled()); // the action has settled
    await view.fixture.whenStable();
    expect(view.submitButton().disabled).toBe(false);
  });

  it('a second submit while pending does not log in twice', async () => {
    auth.login.mockReturnValue(new Promise(() => undefined));
    const view = await filledIn();

    await view.submit();
    await view.submit();

    expect(auth.login).toHaveBeenCalledTimes(1);
  });

  it('explains a redirect caused by an expired session', async () => {
    const { element } = await render({ reason: 'expired' });

    expect(element.textContent).toContain(AUTH_MESSAGES.sessionExpired);
  });

  it('says nothing about sessions on a normal visit', async () => {
    const { element } = await render();

    expect(element.textContent).not.toContain(AUTH_MESSAGES.sessionExpired);
  });

  it('the show-password toggle reveals and hides the password', async () => {
    const view = await render();
    const toggle = view.element.querySelector<HTMLButtonElement>(
      'button[aria-label="Show password"]',
    )!;

    expect(toggle.getAttribute('aria-pressed')).toBe('false');
    toggle.click();
    await view.fixture.whenStable();

    expect(view.input('Password').type).toBe('text');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
  });

  it('shows the demo hint outside production builds', async () => {
    const { element } = await render();

    expect(element.textContent).toContain('Try Admin / Admin');
  });

  it('links to the registration page', async () => {
    const { element } = await render();

    expect(element.querySelector('a[href="/register"]')).not.toBeNull();
  });
});
