# T02: .NET Aspire orchestration (AppHost and ServiceDefaults)

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** DevOps (development) · **Estimate:** 3 h · **Depends on:** T01

## Goal
Run SQL Server, the API and the Angular UI with one command during development, with a dashboard for logs, traces and metrics.

## Steps
- [ ] Create `HeroRegistry.AppHost` and `HeroRegistry.ServiceDefaults` (Aspire 13 templates).
- [ ] In `AppHost.cs`:
  - `AddSqlServer("sql").WithDataVolume().AddDatabase("HeroRegistryDb")`
  - `AddProject<Projects.HeroRegistry_Api>("api").WithReference(db).WaitFor(db).WithExternalHttpEndpoints()`
  - Add the Angular app (`hero-registry-ui`) as a JavaScript/npm app running `npm start`. Pass it a reference to the API and `WaitFor(api)`.
- [ ] Reference ServiceDefaults from the API and call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` (health checks, OpenTelemetry, service discovery and resilience).
- [ ] In the API, read the connection string with `builder.Configuration.GetConnectionString("HeroRegistryDb")`. The API must **not** reference Aspire hosting packages, so that the same code runs under Docker Compose.
- [ ] In the Angular dev server, proxy `/api` to the API endpoint injected by Aspire (`proxy.conf.js` reading the `services__api__http__0` environment variable). This avoids CORS issues in development.
- [ ] Store development secrets (JWT key, seed passwords) as AppHost parameters or user secrets, not in source control.

## Tests
- [ ] Optional: an `Aspire.Hosting.Testing` smoke test that starts the AppHost and checks that `GET /health` on the API returns 200.

## Done when
- `dotnet run --project HeroRegistry/src/HeroRegistry.AppHost` starts all three resources.
- The dashboard shows `sql`, `api` and `ui` as healthy.
- The UI can call the API through the proxy.
