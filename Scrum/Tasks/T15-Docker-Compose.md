# T15: Docker Compose delivery

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** DevOps (delivery) · **Estimate:** 4 h · **Depends on:** T10, T14 · **Status:** Done

## Goal
Deliver the whole system so that it runs with a single `docker compose up --build`, without needing the .NET SDK or Node.js installed.

## Steps
- [x] API `Dockerfile`:
  - Multi-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` to restore, test (optional) and publish, then `aspnet:10.0` as the runtime.
  - Runs as a non-root user (`USER app`) on port 8080.
- [x] UI `Dockerfile`:
  - Multi-stage build: `node:22-alpine` runs `npm ci` and `npm run build`, then `nginx:alpine` serves `dist/`.
  - `nginx.conf` uses SPA fallback (`try_files $uri /index.html`) and proxies `/api` to `http://api:8080`. Because the UI and the API share the same origin, no CORS is needed.
- [x] `docker-compose.yml` services:
  - `sqlserver`: `mcr.microsoft.com/mssql/server:2022-latest` with `ACCEPT_EULA`, `MSSQL_SA_PASSWORD` from `.env`, a named volume and a healthcheck using `sqlcmd`.
  - `api`: `ConnectionStrings__HeroRegistryDb` and `Jwt__Key` from `.env`. `depends_on: sqlserver (service_healthy)`. Port `5080:8080`.
    - `Jwt__Key` is **required** (at least 32 bytes): the API refuses to start without it (T08). In `.env.example`, leave a clear "replace me" note, and document generating one, for example `openssl rand -base64 48`.
    - `Jwt__Issuer`, `Jwt__Audience` and `Jwt__ExpirationMinutes` have defaults in `appsettings.json`. Override them only if needed.
  - `ui`: `depends_on: api`. Port `8080:80`.
- [x] Add `.env.example` (committed) and add `.env` to `.gitignore`.
- [x] Application Insights ([T02.2](T02.2-Application-Insights-Logging.md)): pass `APPLICATIONINSIGHTS_CONNECTION_STRING` to the `api` service from `.env`. In `.env.example`, leave it **empty**, which disables it. Never use a placeholder: a non-empty invalid value stops the API at startup. Also set `OTEL_SERVICE_NAME=hero-registry-api`, so Application Insights shows a clear cloud role name.
- [x] Add `.dockerignore` files for both images (`bin`, `obj`, `node_modules`, `.angular`).
- [x] The API runs the migrations (T03) and seeding (T03.1) on startup, so no manual database step is needed. Its startup retries cover SQL Server still booting, in addition to `depends_on: service_healthy`.

## Tests
- [x] Manual smoke test on a clean environment:
  - Run `docker compose down -v && docker compose up --build`.
  - Log in as `admin` at http://localhost:8080 and perform CRUD.
- [ ] Optional: a CI job that runs compose and curls `/health` (not done; a candidate for the CI task).
- [x] `api` healthcheck: `/health` is anonymous and mapped in every environment (T11). Use it in compose, for example `curl -f http://localhost:8080/health`, if curl is added to the image, or a small `dotnet` probe. Make `ui` `depends_on: api (service_healthy)`.

## Implementation notes

### Files
| File | Purpose |
| ---- | ------- |
| `docker-compose.yml` (repository root) | `sqlserver`, `api` and `ui`, the `sqlserver-data` volume, and the healthchecks |
| `.env.example` (committed), `.gitignore` (root) | Every variable documented; `.env` is ignored |
| `HeroRegistry/src/HeroRegistry.Api/Dockerfile` | Existing template, tidied: no HTTPS port in the container, one `publish` step, `USER $APP_UID` (non-root) |
| `HeroRegistry/.dockerignore` | Also excludes `tests` and `TestResults` (smaller build context) |
| `hero-registry-ui/Dockerfile`, `.dockerignore` | `node:22-alpine` (`npm ci`, `npm run build`), then `nginx:alpine` |
| `hero-registry-ui/nginx/default.conf`, `security-headers.conf` | SPA fallback, `/api` proxied to `http://api:8080`, caching, security headers |

### Decisions
- **API healthcheck without curl:** the `aspnet` image has no curl, and adding it would grow the attack surface. The probe sends `GET /health` over bash's `/dev/tcp` and checks for `200`. `/health` includes the SQL Server check, so "healthy" means "ready".
- **SQL Server healthcheck:** `sqlcmd` from `mssql-tools18` with `-C` (trust the container's self-signed certificate). `$$` keeps the password for the container's shell instead of Compose interpolation.
- **Required variables fail fast:** `${SA_PASSWORD:?…}` and `${JWT__KEY:?…}` make `docker compose` refuse to start, with a message naming the missing variable, before anything is built.
- **Application Insights:** `${APPLICATIONINSIGHTS_CONNECTION_STRING:-}` is empty by default, which disables it (T02.2). `OTEL_SERVICE_NAME=hero-registry-api` sets the cloud role name.
- **SQL Server port:** published only on `127.0.0.1:14333`, for SQL tools on the developer's machine (as the README already documented), and never on the network.
- **Variable names** follow the README (`SA_PASSWORD`, `JWT__KEY`). The connection string uses `Encrypt=True;TrustServerCertificate=True`, because the container's certificate is self-signed.
- **nginx:**
  - `index.html` is `no-cache` and hashed bundles are cached for a year, so a new deployment is picked up at once.
  - The security headers (`nosniff`, `DENY` framing, referrer and permissions policies) live in one snippet included in every location, because nginx does not inherit `add_header` into a location that has its own.
  - `server_tokens off` hides the nginx version.
- **HTTPS:** the stack is plain HTTP on `localhost`, where browsers accept the `Secure` refresh cookie. Any other host needs TLS in front of nginx (documented in the README).
- **Tests are not run inside the API image:** the integration tests need Docker themselves (Testcontainers). The optional CI job (below) is the place for them.

### Smoke test (clean environment)
`docker compose down -v && docker compose up --build`, with throwaway secrets passed only through the shell environment (no `.env` written):
- **Startup:** both images built, and the services became healthy in order: SQL Server → API → UI.
- **API:** `GET /health` returned `Healthy`. The API created `HeroRegistryDb`, migrated it and seeded it by itself.
- **Through nginx (port 8080):**
  - `/` and the deep link `/heroes/7` served the app, with the security headers.
  - Admin login set the `hr_refresh` cookie (HttpOnly, Secure, SameSite=Strict, `Path=/api/auth`). The three seeded heroes were listed.
  - Create returned 201, update 200 and the cookie refresh 200.
- **Persistence:** after `docker compose down` and `up` (volume kept), the hero created before the restart was still there. The migrator reported "0 of 4 migration scripts applied by this run". Delete then returned 204, and the hero was gone (404).
- **Browser (Chrome, production build):** signed out, `/heroes` went to `/login?returnUrl=%2Fheroes`. After signing in, a full page load restored the session from the cookie and showed the seeded heroes. No console errors at all; the dev-only messages of T12 to T14 are absent from the production build.
- **Cleanup:** `docker compose down -v`. The volume must go too, because SQL Server keeps the `sa` password from its first start in the volume; a later `.env` with another password would lock the API out.

## Done when
- The stack starts from scratch with one command, the seeded data is visible, and restarting keeps the data (volume).
