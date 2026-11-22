# T02: .NET Aspire orchestration (AppHost and ServiceDefaults)

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** DevOps (development) · **Estimate:** 3 h · **Depends on:** T01 · **Status:** Done

## Goal
Run SQL Server, the API and the Angular UI with one command during development, with a dashboard for logs, traces and metrics.

## Steps
- [x] Create `HeroRegistry.AppHost` and `HeroRegistry.ServiceDefaults` (Aspire 13).
- [x] In `AppHost.cs`:
  - `AddSqlServer("sql").WithDataVolume().AddDatabase("HeroRegistryDb")`
  - `AddProject<Projects.HeroRegistry_Api>("api").WithReference(db).WaitFor(db).WithExternalHttpEndpoints()`
  - Add the Angular app (`hero-registry-ui`) as a JavaScript/npm app running `npm start`. Pass it a reference to the API and `WaitFor(api)`.
- [x] Reference ServiceDefaults from the API and call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` (health checks, OpenTelemetry, service discovery and resilience).
- [x] In the API, read the connection string with `builder.Configuration.GetConnectionString("HeroRegistryDb")`. The API must **not** reference Aspire hosting packages, so that the same code runs under Docker Compose.
- [x] In the Angular dev server, proxy `/api` to the API endpoint injected by Aspire (`proxy.conf.mjs` reading the `services__api__http__0` environment variable). This avoids CORS issues in development.
- [x] Store development secrets (JWT key, seed passwords) as AppHost parameters or user secrets, not in source control.

## Tests
- [x] Unit and integration tests for the new SQL Server health check and the connection string binding (Infrastructure.Tests, with Testcontainers).
- [x] API tests for `/alive` (200) and `/health` (503 when no database is configured).
- [ ] Optional: an `Aspire.Hosting.Testing` smoke test. **Not added**, see the notes.

## Done when
- [x] `dotnet run --project HeroRegistry/src/HeroRegistry.AppHost` starts all three resources.
- [x] The dashboard shows `sql`, `api` and `ui` as running, and the API's `/health` (which includes SQL Server) returns `Healthy`.
- [x] The UI can call the API through the proxy. `GET http://localhost:4200/api/...` is answered by the API's Kestrel server.

## Implementation notes
- **Versions:**
  - Aspire **13.6.5**. Aspire 17.0.0 was released 4 days before this task, so it was left for a later, deliberate upgrade.
  - Microsoft.Extensions resilience and service discovery **10.11.0**.
  - OpenTelemetry **1.20.1**.
- **Orchestrator source:** `AspireUseCliBundle=false`. DCP and the dashboard come from NuGet packages, so the AppHost works with `dotnet run`, Visual Studio and CI regardless of the installed Aspire CLI version (13.1.2 locally). `ASPIRE010` is suppressed because it only reports that choice.
- **SQL Server:**
  - Uses the named volume `hero-registry-sql-data` and `ContainerLifetime.Persistent`, so the container survives AppHost restarts.
  - Aspire generates the SA password (`Parameters:sql-password` in user secrets).
- **JWT key:** `AddParameter("jwt-key", new GenerateParameterDefault { MinLength = 64 }, secret: true, persist: true)` generates a random key on first run and persists it in the AppHost's user secrets. The key is injected as `Jwt__Key` and will be consumed in T08. Seed passwords are documented demo credentials (README), so T03 decides how to supply them.
- **UI:**
  - `AddJavaScriptApp("ui", "../../../hero-registry-ui", "start").WithNpm(install: true)` runs `npm install` automatically before `ng serve`.
  - Fixed port **4200** (`isProxied: false`), so the URL is stable and matches the future CORS origin (T11).
  - Angular analytics prompts are disabled with `NG_CLI_ANALYTICS=false`.
- **Proxy:** `hero-registry-ui/proxy.conf.mjs` is set in `angular.json` → `serve.options.proxyConfig`. It falls back to `http://localhost:5103` when run without Aspire.
- **Health checks:**
  - Infrastructure registers `SqlServerHealthCheck` (`SELECT 1`). It reports failures and never throws.
  - `/health` checks everything (readiness). `/alive` checks only the `live` tag (liveness).
  - The AppHost uses `WithHttpHealthCheck("/health")`, so the dashboard reflects database connectivity.
  - Both endpoints are mapped only in Development for now. Exposing them in Docker Compose is part of T11 and T15.
- **Configuration:** `DatabaseOptions` is bound from `ConnectionStrings:HeroRegistryDb` (empty when missing). Aspire supplies it in development, and Docker Compose will supply it in delivery.
- **Testcontainers fixture:** `SqlServerContainerFixture` is an xUnit v3 assembly fixture. One SQL Server 2022 container is shared by all Infrastructure integration tests and will be reused in T03, T05 and T06.
- **Optional Aspire smoke test (not added):** `Aspire.Hosting.Testing` would start SQL Server, the API and `npm install` + `ng serve` on every `dotnet test` run (and in CI, which would then need Node.js). The same checks were verified manually instead: `/health` returned `Healthy`, the UI returned 200, and the proxy reached Kestrel. This can be added later as a separate, opt-in test project.
- **Dockerfile:** now also copies `HeroRegistry.ServiceDefaults.csproj` for the restore layer.
- **Test count:** 46 tests, all passing.
