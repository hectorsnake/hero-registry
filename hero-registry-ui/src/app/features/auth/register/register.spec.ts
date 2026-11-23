import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RegisterRequest } from '../../../core/auth/auth-models';
import { AuthService } from '../../../core/auth/auth-service';
import { AUTH_MESSAGES } from '../auth-schemas';
import { Register } from './register';

const STRONG = 'Sp1der-Sense!';

function problem(status: number, errors?: Record<string, string[]>, title = 'Conflict') {
  return new HttpErrorResponse({
    status,
    error: { title, status, code: 'user.duplicate', errors },
  });
}

describe('Register', () => {
  const auth = {
    register: vi.fn<(request: RegisterRequest) => Promise<unknown>>(),
    isAuthenticated: signal(false),
  };
  let router: Router;

  beforeEach(() => {
    auth.register.mockReset().mockResolvedValue({ id: 2, userName: 'peter.parker' });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  async function render() {
    const fixture = TestBed.createComponent(Register);
    await fixture.whenStable();
    return page(fixture);
  }

  function page(fixture: ComponentFixture<Register>) {
    const element = fixture.nativeElement as HTMLElement;
    const input = (label: string) => {
      const id = Array.from(element.querySelectorAll('label')).find(
        (l) => l.textContent?.trim() === label,
      )?.htmlFor;
      return element.querySelector<HTMLInputElement>(`#${id}`)!;
    };
    const errorFor = (label: string) => {
      // aria-describedby may list several ids (the error first, then for example the checklist).
      const describedBy = input(label).getAttribute('aria-describedby')?.split(' ')[0];
      return element.querySelector(`#${describedBy}`)?.textContent?.trim() ?? '';
    };
    const view = {
      fixture,
      element,
      input,
      errorFor,
      submitButton: () => element.querySelector<HTMLButtonElement>('button[type="submit"]')!,
      checklist: () =>
        Object.fromEntries(
          Array.from(element.querySelectorAll('[data-rule]')).map((li) => [
            li.getAttribute('data-rule'),
            li.getAttribute('data-met') === 'true',
          ]),
        ),
      async type(label: string, value: string) {
        const field = input(label);
        field.value = value;
        field.dispatchEvent(new Event('input'));
        field.dispatchEvent(new Event('blur'));
        await fixture.whenStable();
      },
      async submit() {
        element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
        await fixture.whenStable();
        await vi.waitFor(() => expect(view.submitButton().getAttribute('aria-busy')).toBe('false'));
        await fixture.whenStable();
      },
      async fillIn(values: Partial<Record<string, string>> = {}) {
        const all = {
          'User name': 'peter.parker',
          Email: 'peter@dailybugle.com',
          Password: STRONG,
          'Confirm password': STRONG,
          ...values,
        };
        for (const [label, value] of Object.entries(all)) {
          await view.type(label, value ?? '');
        }
      },
    };
    return view;
  }

  it('labels every input with the right autocomplete hint', async () => {
    const { input } = await render();

    expect(input('User name').autocomplete).toBe('username');
    expect(input('Email').autocomplete).toBe('email');
    expect(input('Password').autocomplete).toBe('new-password');
    expect(input('Confirm password').autocomplete).toBe('new-password');
  });

  it('submitting an invalid form shows the errors and focuses the first invalid field', async () => {
    const view = await render();
    await view.type('User name', 'peter.parker');

    await view.submit();

    expect(auth.register).not.toHaveBeenCalled();
    expect(view.errorFor('Email')).toBe(AUTH_MESSAGES.required);
    expect(document.activeElement).toBe(view.input('Email'));
    expect(view.submitButton().disabled).toBe(false);
  });

  it('the password checklist reacts to typing', async () => {
    const view = await render();
    expect(Object.values(view.checklist()).every((met) => !met)).toBe(true);

    await view.type('Password', 'abcdefgh');
    expect(view.checklist()).toEqual({
      length: true,
      uppercase: false,
      lowercase: true,
      digit: false,
      symbol: false,
    });

    await view.type('Password', STRONG);
    expect(Object.values(view.checklist()).every((met) => met)).toBe(true);
  });

  it('shows a mismatch on Confirm password', async () => {
    const view = await render();

    await view.fillIn({ 'Confirm password': 'Different-pass1' });

    expect(view.errorFor('Confirm password')).toBe(AUTH_MESSAGES.passwordMismatch);
    await view.submit();
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('registers (without confirmPassword), which signs in, then goes to /heroes', async () => {
    const view = await render();
    await view.fillIn();

    await view.submit();

    expect(auth.register).toHaveBeenCalledExactlyOnceWith({
      userName: 'peter.parker',
      email: 'peter@dailybugle.com',
      password: STRONG,
    });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/heroes');
  });

  it('a 409 with errors.userName puts the error on the userName field and focuses it', async () => {
    auth.register.mockRejectedValue(
      problem(409, { userName: ['This user name is already taken.'] }),
    );
    const view = await render();
    await view.fillIn();

    await view.submit();

    expect(view.errorFor('User name')).toBe('This user name is already taken.');
    expect(view.input('User name').getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(view.input('User name'));
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('editing the field clears its server error', async () => {
    auth.register.mockRejectedValue(
      problem(409, { userName: ['This user name is already taken.'] }),
    );
    const view = await render();
    await view.fillIn();
    await view.submit();

    await view.type('User name', 'peter.parker2');

    expect(view.errorFor('User name')).toBe('');
    expect(view.input('User name').getAttribute('aria-invalid')).toBe('false');
  });

  it('a 400 maps every returned field', async () => {
    auth.register.mockRejectedValue(
      problem(
        400,
        {
          userName: ['UserName can only contain letters.'],
          email: ['Email is not valid.'],
          password: ['Password must contain a symbol.'],
          body: ['ignored: not a field'],
        },
        'One or more validation errors occurred.',
      ),
    );
    const view = await render();
    await view.fillIn();

    await view.submit();

    expect(view.errorFor('User name')).toBe('UserName can only contain letters.');
    expect(view.errorFor('Email')).toBe('Email is not valid.');
    expect(view.errorFor('Password')).toBe('Password must contain a symbol.');
  });

  it('a problem without field errors shows its title', async () => {
    auth.register.mockRejectedValue(
      problem(409, undefined, 'An account already uses some of these values.'),
    );
    const view = await render();
    await view.fillIn();

    await view.submit();

    expect(view.element.querySelector('[aria-live="polite"]')?.textContent?.trim()).toBe(
      'An account already uses some of these values.',
    );
  });

  it('links back to the login page', async () => {
    const { element } = await render();

    expect(element.querySelector('a[href="/login"]')).not.toBeNull();
  });
});
