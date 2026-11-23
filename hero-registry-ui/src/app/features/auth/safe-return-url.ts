/** Where to go after signing in when there is no (safe) returnUrl. */
export const DEFAULT_AFTER_LOGIN = '/heroes';

/**
 * Only same-app paths: `/heroes/7` yes; `https://evil.com`, `//evil.com` (protocol-relative) and
 * `/\evil.com` (browsers read `\` as `/`) no. Prevents open redirects through `?returnUrl=`.
 */
export function safeReturnUrl(returnUrl: string | null | undefined): string {
  const isAppPath =
    !!returnUrl &&
    returnUrl.startsWith('/') &&
    !returnUrl.startsWith('//') &&
    !returnUrl.startsWith('/\\');

  return isAppPath ? returnUrl : DEFAULT_AFTER_LOGIN;
}
