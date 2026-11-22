# T01: Solution restructure and Clean Architecture skeleton

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** All · **Estimate:** 3 h · **Depends on:** none

## Goal
Turn the current single `HeroRegistry` Web API template into a Clean Architecture solution with separate source and test projects, and enforce the dependency rule.

## Steps
- [ ] Add a root `.gitignore` (VisualStudio and Node templates) so that `bin/`, `obj/`, `.vs/`, `node_modules/`, `dist/` and `*.user` are excluded.
- [ ] Remove the nested `hero-registry-ui/.git` folder so the UI is part of the main repository (no accidental submodule).
- [ ] Move the existing project to `HeroRegistry/src/HeroRegistry.Api` and rename it. Delete `WeatherForecast.cs`.
- [ ] Create the class libraries (`net10.0`):
  - `HeroRegistry.Domain`: no project or package references
  - `HeroRegistry.Application`: references Domain
  - `HeroRegistry.Infrastructure`: references Application and `Microsoft.Data.SqlClient`
  - `HeroRegistry.Api`: references Application and Infrastructure (Infrastructure is used for DI composition only)
- [ ] Create the xUnit test projects under `HeroRegistry/tests/`:
  - `HeroRegistry.Domain.Tests`
  - `HeroRegistry.Application.Tests` (NSubstitute)
  - `HeroRegistry.Infrastructure.Tests` (Testcontainers.MsSql)
  - `HeroRegistry.Api.Tests` (Microsoft.AspNetCore.Mvc.Testing)
  - All of them use Shouldly and coverlet.collector.
- [ ] Add `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true` (src only) and `AnalysisLevel=latest`.
- [ ] Add `Directory.Packages.props` (Central Package Management).
- [ ] Register every project in `HeroRegistry.slnx` using `src` and `tests` solution folders.
- [ ] Add a `DependencyInjection.cs` extension in Application (`AddApplication()`) and in Infrastructure (`AddInfrastructure(IConfiguration)`).

## Tests (TDD)
- [ ] Architecture test (in `Application.Tests`, using Reflection or NetArchTest):
  - Domain references no other project and no third-party packages.
  - Application does not reference Infrastructure, Api or `Microsoft.Data.SqlClient`.
- [ ] Verify that no project references `EntityFramework`, `Dapper` or `MediatR`.

## Done when
- `dotnet build HeroRegistry/HeroRegistry.slnx` completes with 0 warnings.
- `dotnet test` runs, and the architecture tests pass.
