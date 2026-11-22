# T15: Docker Compose delivery

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** DevOps (delivery) · **Estimate:** 4 h · **Depends on:** T10, T14

## Goal
Deliver the whole system so that it runs with a single `docker compose up --build`, without needing the .NET SDK or Node.js installed.

## Steps
- [ ] API `Dockerfile`:
  - Multi-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` to restore, test (optional) and publish, then `aspnet:10.0` as the runtime.
  - Runs as a non-root user (`USER app`) on port 8080.
- [ ] UI `Dockerfile`:
  - Multi-stage build: `node:22-alpine` runs `npm ci` and `npm run build`, then `nginx:alpine` serves `dist/`.
  - `nginx.conf` uses SPA fallback (`try_files $uri /index.html`) and proxies `/api` to `http://api:8080`. Because the UI and the API share the same origin, no CORS is needed.
- [ ] `docker-compose.yml` services:
  - `sqlserver`: `mcr.microsoft.com/mssql/server:2022-latest` with `ACCEPT_EULA`, `MSSQL_SA_PASSWORD` from `.env`, a named volume and a healthcheck using `sqlcmd`.
  - `api`: `ConnectionStrings__HeroRegistryDb` and `Jwt__Key` from `.env`. `depends_on: sqlserver (service_healthy)`. Port `5080:8080`.
    - `Jwt__Key` is **required** (at least 32 bytes): the API refuses to start without it (T08). In `.env.example`, leave a clear "replace me" note, and document generating one, for example `openssl rand -base64 48`.
    - `Jwt__Issuer`, `Jwt__Audience` and `Jwt__ExpirationMinutes` have defaults in `appsettings.json`. Override them only if needed.
  - `ui`: `depends_on: api`. Port `8080:80`.
- [ ] Add `.env.example` (committed) and add `.env` to `.gitignore`.
- [ ] Application Insights ([T02.2](T02.2-Application-Insights-Logging.md)): pass `APPLICATIONINSIGHTS_CONNECTION_STRING` to the `api` service from `.env`. In `.env.example`, leave it **empty**, which disables it. Never use a placeholder: a non-empty invalid value stops the API at startup. Also set `OTEL_SERVICE_NAME=hero-registry-api`, so Application Insights shows a clear cloud role name.
- [ ] Add `.dockerignore` files for both images (`bin`, `obj`, `node_modules`, `.angular`).
- [ ] The API runs the migrations (T03) and seeding (T03.1) on startup, so no manual database step is needed. Its startup retries cover SQL Server still booting, in addition to `depends_on: service_healthy`.

## Tests
- [ ] Manual smoke test on a clean environment:
  - Run `docker compose down -v && docker compose up --build`.
  - Log in as `admin` at http://localhost:8080 and perform CRUD.
- [ ] Optional: a CI job that runs compose and curls `/health`.
- [ ] `api` healthcheck: `/health` is anonymous and mapped in every environment (T11). Use it in compose, for example `curl -f http://localhost:8080/health`, if curl is added to the image, or a small `dotnet` probe. Make `ui` `depends_on: api (service_healthy)`.

## Done when
- The stack starts from scratch with one command, the seeded data is visible, and restarting keeps the data (volume).
