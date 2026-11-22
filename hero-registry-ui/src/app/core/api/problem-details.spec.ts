import { HttpErrorResponse } from '@angular/common/http';
import { isProblemDetails, problemTitle, toFieldErrors } from './problem-details';

const HERO_FIELDS = ['heroName', 'email', 'superpowers'] as const;

function validationProblem(errors: Record<string, string[]>) {
  return {
    type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
    title: 'One or more validation errors occurred.',
    status: 400,
    code: 'validation.failed',
    traceId: '00-abc-def-00',
    errors,
  };
}

describe('toFieldErrors', () => {
  it('maps errors.heroName to the heroName field', () => {
    const problem = validationProblem({ heroName: ['HeroName is required.'] });

    expect(toFieldErrors(problem, HERO_FIELDS)).toEqual({ heroName: ['HeroName is required.'] });
  });

  it('keeps every message of every known field', () => {
    const problem = validationProblem({
      heroName: ['Too short.', 'Already taken.'],
      email: ['Invalid email.'],
    });

    expect(toFieldErrors(problem, HERO_FIELDS)).toEqual({
      heroName: ['Too short.', 'Already taken.'],
      email: ['Invalid email.'],
    });
  });

  it('ignores keys that are not fields of the form', () => {
    const problem = validationProblem({
      body: ['The request body is not valid JSON.'],
      heroName: ['x'],
    });

    expect(toFieldErrors(problem, HERO_FIELDS)).toEqual({ heroName: ['x'] });
  });

  it('maps an item path such as superpowers[0] to its field', () => {
    const problem = validationProblem({ 'superpowers[0]': ['A superpower is required.'] });

    expect(toFieldErrors(problem, HERO_FIELDS)).toEqual({
      superpowers: ['A superpower is required.'],
    });
  });

  it('returns no field errors for a problem without errors', () => {
    expect(toFieldErrors({ title: 'Not found', status: 404 }, HERO_FIELDS)).toEqual({});
  });

  it('reads the problem out of an HttpErrorResponse', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: validationProblem({ email: ['Taken.'] }),
    });

    expect(toFieldErrors(error, HERO_FIELDS)).toEqual({ email: ['Taken.'] });
  });

  it.each([null, undefined, 'Internal Server Error', 42, { errors: 'nope' }])(
    'returns no field errors for a body that is not a problem (%s)',
    (body) => {
      expect(toFieldErrors(body, HERO_FIELDS)).toEqual({});
    },
  );
});

describe('isProblemDetails', () => {
  it('recognizes an RFC 9457 problem by its title and status', () => {
    expect(isProblemDetails({ title: 'Forbidden', status: 403 })).toBe(true);
  });

  it.each([null, 'text', { title: 'x' }, { status: 500 }])('rejects %s', (value) => {
    expect(isProblemDetails(value)).toBe(false);
  });
});

describe('problemTitle', () => {
  it('uses the title of the problem body', () => {
    const error = new HttpErrorResponse({
      status: 500,
      error: { title: 'An unexpected error occurred.', status: 500 },
    });

    expect(problemTitle(error, 'Fallback')).toBe('An unexpected error occurred.');
  });

  it('falls back when the body is raw server text, never showing it', () => {
    const error = new HttpErrorResponse({
      status: 502,
      error: '<html>Bad Gateway from nginx</html>',
    });

    expect(problemTitle(error, 'Something went wrong.')).toBe('Something went wrong.');
  });
});
