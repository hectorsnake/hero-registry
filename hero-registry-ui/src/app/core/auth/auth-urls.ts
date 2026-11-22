/** The API is same-origin: `/api` is proxied by `ng serve` (Aspire) and by nginx (Docker Compose). */
export const API_PREFIX = '/api/';

export const AUTH_URLS = {
  login: '/api/auth/login',
  register: '/api/auth/register',
  refresh: '/api/auth/refresh',
  logout: '/api/auth/logout',
  me: '/api/auth/me',
} as const;

const TOKEN_ENDPOINTS: readonly string[] = [
  AUTH_URLS.login,
  AUTH_URLS.register,
  AUTH_URLS.refresh,
  AUTH_URLS.logout,
];

/** A request to this app's API (relative `/api/...`), never to a third party. */
export function isApiRequest(url: string): boolean {
  return url.startsWith(API_PREFIX);
}

/** The endpoints that handle tokens themselves: no bearer header and no refresh-and-retry. */
export function isTokenEndpoint(url: string): boolean {
  const path = url.split(/[?#]/, 1)[0];
  return TOKEN_ENDPOINTS.includes(path);
}
