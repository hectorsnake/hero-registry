import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ValidationError, apply, form } from '@angular/forms/signals';
import {
  AUTH_MESSAGES,
  emailSchema,
  errorMessage,
  passwordChecklist,
  passwordPolicySchema,
  registerSchema,
  RegisterModel,
  userNameSchema,
} from './auth-schemas';

function kinds(errors: readonly ValidationError[]): string[] {
  return errors.map((e) => e.kind);
}

function validateUserName(value: string): readonly ValidationError[] {
  const f = TestBed.runInInjectionContext(() =>
    form(signal({ userName: value }), (s) => apply(s.userName, userNameSchema)),
  );
  return f.userName().errors();
}

function validateEmail(value: string): readonly ValidationError[] {
  const f = TestBed.runInInjectionContext(() =>
    form(signal({ email: value }), (s) => apply(s.email, emailSchema)),
  );
  return f.email().errors();
}

function validatePassword(value: string): readonly ValidationError[] {
  const f = TestBed.runInInjectionContext(() =>
    form(signal({ password: value }), (s) => apply(s.password, passwordPolicySchema)),
  );
  return f.password().errors();
}

function registerForm(model: Partial<RegisterModel>) {
  return TestBed.runInInjectionContext(() =>
    form(
      signal<RegisterModel>({
        userName: 'peter.parker',
        email: 'peter@dailybugle.com',
        password: 'Sp1der-Sense!',
        confirmPassword: 'Sp1der-Sense!',
        ...model,
      }),
      registerSchema,
    ),
  );
}

describe('userNameSchema', () => {
  it('is required', () => {
    expect(kinds(validateUserName(''))).toEqual(['required']);
  });

  it.each([
    ['ab', ['minLength']], // 2: too short
    ['abc', []], // 3: the minimum
    ['a'.repeat(50), []], // 50: the maximum
    ['a'.repeat(51), ['maxLength']], // 51: too long
  ])('length boundary: %s', (value, expected) => {
    expect(kinds(validateUserName(value))).toEqual(expected);
  });

  it.each(['peter.parker', 'spider_man-2099', 'ABC'])('accepts %s', (value) => {
    expect(validateUserName(value)).toEqual([]);
  });

  it.each(['peter parker', 'peter@parker', 'péter'])(
    'rejects characters outside the pattern: %s',
    (value) => {
      expect(kinds(validateUserName(value))).toEqual(['pattern']);
    },
  );

  it('explains every rule with a message from the message map', () => {
    expect(validateUserName('ab')[0].message).toBe(AUTH_MESSAGES.userNameLength);
    expect(validateUserName('bad name')[0].message).toBe(AUTH_MESSAGES.userNamePattern);
  });
});

describe('emailSchema', () => {
  it('is required', () => {
    expect(kinds(validateEmail(''))).toEqual(['required']);
  });

  it.each(['not-an-email', 'peter@', '@dailybugle.com'])('rejects %s', (value) => {
    expect(kinds(validateEmail(value))).toEqual(['email']);
  });

  it('accepts a valid address', () => {
    expect(validateEmail('peter@dailybugle.com')).toEqual([]);
  });
});

describe('passwordPolicySchema', () => {
  it('accepts a password that meets every rule', () => {
    expect(validatePassword('Sp1der-Sense!')).toEqual([]);
  });

  it('is required', () => {
    expect(kinds(validatePassword(''))).toContain('required');
  });

  it('needs at least 8 characters (only that rule fails)', () => {
    expect(kinds(validatePassword('Ab1!xyz'))).toEqual(['minLength']);
  });

  it('allows at most 64 characters, like the server', () => {
    expect(kinds(validatePassword(`Ab1!${'x'.repeat(61)}`))).toEqual(['maxLength']);
    expect(validatePassword(`Ab1!${'x'.repeat(60)}`)).toEqual([]);
  });

  it.each([
    ['abcdef1!', 'uppercase'],
    ['ABCDEF1!', 'lowercase'],
    ['Abcdefg!', 'digit'],
    ['Abcdefg1', 'symbol'],
  ])('%s fails only the %s rule', (value, rule) => {
    const errors = validatePassword(value);

    expect(errors).toEqual([expect.objectContaining({ kind: 'password.policy', rule })]);
    expect(errors[0].message).toBeTruthy();
  });

  it('reports one error per missing rule, for the checklist', () => {
    expect(validatePassword('abcdefgh').map((e) => (e as { rule?: string }).rule)).toEqual([
      'uppercase',
      'digit',
      'symbol',
    ]);
  });

  it('counts non-ASCII letters, like the server (char.IsUpper)', () => {
    expect(validatePassword('Ñandú-2099')).toEqual([]);
  });
});

describe('registerSchema', () => {
  it('a valid registration has no errors', () => {
    expect(registerForm({})().errors()).toEqual([]);
    expect(registerForm({})().valid()).toBe(true);
  });

  it('a password mismatch puts the error on confirmPassword, not on password', () => {
    const f = registerForm({ confirmPassword: 'Something-else1' });

    expect(kinds(f.confirmPassword().errors())).toEqual(['password.mismatch']);
    expect(f.confirmPassword().errors()[0].message).toBe(AUTH_MESSAGES.passwordMismatch);
    expect(f.password().errors()).toEqual([]);
  });

  it('applies the user name, email and password rules', () => {
    const f = registerForm({
      userName: 'x',
      email: 'nope',
      password: 'weak',
      confirmPassword: 'weak',
    });

    expect(f.userName().invalid()).toBe(true);
    expect(f.email().invalid()).toBe(true);
    expect(f.password().invalid()).toBe(true);
  });
});

describe('passwordChecklist', () => {
  it('marks every rule unmet for an empty password', () => {
    expect(passwordChecklist(validatePassword('')).every((item) => !item.met)).toBe(true);
  });

  it('marks exactly the rules that are met', () => {
    const checklist = passwordChecklist(validatePassword('abcdefgh'));

    expect(Object.fromEntries(checklist.map((item) => [item.id, item.met]))).toEqual({
      length: true,
      uppercase: false,
      lowercase: true,
      digit: false,
      symbol: false,
    });
  });

  it('lists the rules with labels in a fixed order', () => {
    expect(passwordChecklist([]).map((item) => item.id)).toEqual([
      'length',
      'uppercase',
      'lowercase',
      'digit',
      'symbol',
    ]);
    expect(passwordChecklist([]).every((item) => item.met && item.label.length > 0)).toBe(true);
  });
});

describe('errorMessage', () => {
  it('uses the message carried by the error', () => {
    expect(errorMessage({ kind: 'server', message: 'This user name is already taken.' })).toBe(
      'This user name is already taken.',
    );
  });

  it('falls back to the message map by kind', () => {
    expect(errorMessage({ kind: 'required' })).toBe(AUTH_MESSAGES.required);
  });

  it('has a generic message for unknown kinds', () => {
    expect(errorMessage({ kind: 'mystery' })).toBe(AUTH_MESSAGES.invalid);
  });
});
