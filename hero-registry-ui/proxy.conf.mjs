// Development proxy for `ng serve`. Forwards /api calls to the HeroRegistry API,
// so the browser sees one origin (no CORS needed in development).
//
// Under .NET Aspire, `WithReference(api)` injects the API address as environment variables:
//   services__api__https__0 / services__api__http__0
//
// Prefer HTTPS. When the API also has an HTTPS endpoint, UseHttpsRedirection answers every HTTP request
// with a 307 to https://localhost:<port>. The browser would follow it to another origin, where it drops
// the Authorization header and ignores the refresh cookie's Set-Cookie, so every sign-in would end at
// /login?reason=expired.
//
// Outside Aspire (plain `npm start`), it falls back to the API's launchSettings "http" profile, which
// has no HTTPS port and therefore never redirects.

const target =
  process.env['services__api__https__0'] ??
  process.env['services__api__http__0'] ??
  'http://localhost:5103';

export default {
  '/api': {
    target,
    secure: false, // accept the ASP.NET Core development certificate
    changeOrigin: true,
    logLevel: 'info',
  },
};
