# T16: CI pipeline (GitHub Actions)

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** DevOps · **Estimate:** 2 h · **Depends on:** T01

## Goal
Automatically build and test the back end and front end on every push and pull request.

## Steps
- [ ] `.github/workflows/ci.yml`, triggered on `push` to `main` and on `pull_request`.
- [ ] Job `backend` (ubuntu-latest, which has Docker for Testcontainers):
  - `actions/setup-dotnet` (10.0.x)
  - `dotnet restore`, `dotnet build -c Release --no-restore`
  - `dotnet test --solution HeroRegistry/HeroRegistry.slnx -c Release --no-build --coverage --coverage-output-format cobertura` (Microsoft.Testing.Platform, enabled by `global.json`)
  - Upload the test results and coverage report as artifacts.
- [ ] Job `frontend`:
  - `actions/setup-node` (22, npm cache)
  - `npm ci`, `npm run build`, then `npm test -- --watch=false`
- [ ] Optional job `docker`: `docker compose build` to make sure the images build.
- [ ] Add a status badge to the README.

## Done when
- The pipeline is green on `main`, and a failing test makes the pipeline fail.
