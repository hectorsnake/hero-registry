import { isApiRequest, isTokenEndpoint } from './auth-urls';

describe('auth URLs', () => {
  it.each(['/api/heroes', '/api/auth/me', '/api/users/2?x=1'])(
    '%s is a request to this API',
    (url) => {
      expect(isApiRequest(url)).toBe(true);
    },
  );

  it.each(['https://evil.example/api/heroes', '/assets/logo.svg', 'api/heroes', '/apix'])(
    '%s is not a request to this API',
    (url) => {
      expect(isApiRequest(url)).toBe(false);
    },
  );

  it.each([
    '/api/auth/login',
    '/api/auth/register',
    '/api/auth/refresh',
    '/api/auth/logout',
    '/api/auth/refresh?x=1',
  ])('%s handles tokens itself', (url) => {
    expect(isTokenEndpoint(url)).toBe(true);
  });

  it.each(['/api/auth/me', '/api/heroes', '/api/auth/login-history'])(
    '%s is an ordinary endpoint',
    (url) => {
      expect(isTokenEndpoint(url)).toBe(false);
    },
  );
});
