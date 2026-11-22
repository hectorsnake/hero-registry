// Development proxy for `ng serve`. Forwards /api calls to the HeroRegistry API,
// so the browser sees one origin (no CORS needed in development).
//
// Under .NET Aspire, `WithReference(api)` injects the API address as environment variables:
//   services__api__http__0 / services__api__https__0
// Outside Aspire (plain `npm start`), it falls back to the API's launchSettings "http" URL.

const target =
  process.env['services__api__http__0'] ??
  process.env['services__api__https__0'] ??
  'http://localhost:5103';

export default {
  '/api': {
    target,
    secure: false, // accept the ASP.NET Core development certificate
    changeOrigin: true,
    logLevel: 'info',
  },
};
