# T16: CI pipeline (GitHub Actions)

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** DevOps · **Estimate:** 2 h · **Depends on:** T01 · **Status:** Done (pending the first green run on GitHub)

## Goal
Automatically build and test the back end and front end on every push and pull request.

## Steps
- [x] `.github/workflows/ci.yml`, triggered on `push` to `main` and on `pull_request`.
- [x] Job `backend` (ubuntu-latest, which has Docker for Testcontainers):
  - `actions/setup-dotnet` (10.0.x)
  - `dotnet restore`, `dotnet build -c Release --no-restore`
  - `dotnet test --solution HeroRegistry/HeroRegistry.slnx -c Release --no-build --coverage --coverage-output-format cobertura` (Microsoft.Testing.Platform, enabled by `global.json`)
  - Upload the test results and coverage report as artifacts.
- [x] Job `frontend`:
  - `actions/setup-node` (22, npm cache)
  - `npm ci`, `npm run build`, then `npm test -- --watch=false`
- [x] Optional job `docker`: `docker compose build` to make sure the images build.
- [x] Add a status badge to the README.

## Implementation notes

### `.github/workflows/ci.yml`
- **Triggers:** `push` to `main`, every `pull_request`, and `workflow_dispatch` (a manual "Run workflow" button).
- **Security:** `permissions: contents: read` (least privilege). No repository secrets are needed: the Docker job generates throwaway secrets per run and masks them in the logs.
- **`concurrency`:** a new push to a pull request cancels its previous run; runs on `main` always finish.
- **Actions** are on their current majors (all on the Node 24 runtime): `checkout@v7`, `setup-dotnet@v6`, `setup-node@v7`, `cache@v6`, `upload-artifact@v7`.

| Job | Steps | Notes |
| --- | ----- | ----- |
| `backend` | setup .NET 10, NuGet cache, restore, build (Release), `dotnet test --solution … --coverage --coverage-output-format cobertura --results-directory TestResults`, upload `TestResults/` | The NuGet cache uses `actions/cache` keyed on `Directory.Packages.props` and the project files (setup-dotnet's cache needs lock files). The integration tests use the runner's Docker. |
| `frontend` | setup Node 22 (npm cache), `npm ci`, lint, build, `test:coverage`, upload `coverage/` | Node 22 resolves to the latest 22.x (Angular 22 needs ^22.22.3). The coverage thresholds (80%) fail the job. |
| `docker` (optional in T16) | `needs: [backend, frontend]`, throwaway secrets, `docker compose build`, `up -d --wait`, smoke test, logs on failure, `down -v` | The smoke test covers `/health`, `/` and a deep link through nginx, an Admin login through nginx, and the seeded heroes. Together with T15's optional item, it is the "compose and curl `/health`" job. |

### Verified locally before the first push
- **Back-end commands** (restore, Release build, test with coverage): 0 warnings, **763/763** tests, including the Testcontainers ones.
  - This run found that `dotnet test --solution` writes results to `TestResults/` in the **current directory**, not per project. The workflow therefore passes `--results-directory TestResults` and uploads that folder.
  - The README's coverage comment was corrected, and `TestResults/` was added to the root `.gitignore`.
- **Front-end commands:** lint clean, build clean, **353/353** tests with 97.9% lines and 93.4% branches.
- **Compose smoke test:** the same calls were run by hand in T15 (`up --wait`, `/health`, login through nginx, the hero count). `jq` is preinstalled on `ubuntu-latest`.
- **Workflow syntax:** no offline YAML linter was available here, so GitHub validates it on the first run (the Actions tab shows any syntax error immediately).

### First run on GitHub: a convention test failed only in CI (fixed)
- **Symptom:** `DomainConventionsTests.All_domain_types_live_in_the_Domain_namespace` failed on `ubuntu-latest`, but passed on Windows. It reported the type `Microsoft.CodeCoverage.Instrumentation.Static.Tracker.StaticManagedTrackerTemplate_…`.
- **Cause:** with `--coverage`, Microsoft's code coverage uses **static instrumentation** on Linux. It rewrites the assembly under test and injects its own tracker type into `HeroRegistry.Domain.dll`. On Windows it instruments dynamically, so the type never appeared locally.
- **Reproduced on Windows** by forcing the same mode with a coverage settings file (`EnableStaticManagedInstrumentation=True`, `EnableDynamicManagedInstrumentation=False`): the same test failed with the same type.
- **Fix:** the test ignores types in the `Microsoft.CodeCoverage.` namespace (injected by the tool, not Domain code) and stays strict for everything else.
- **Verified:**
  - the Domain tests are green with and without coverage;
  - a temporary `Elsewhere.MisplacedProbe` type in the Domain project still failed the test, so the rule is intact;
  - the whole solution in static-instrumentation mode passed 763/763.

### README
- A CI badge at the top.
- A "Continuous integration (GitHub Actions)" section under Testing.
- The corrected coverage command.

## Done when
- The pipeline is green on `main`, and a failing test makes the pipeline fail.
