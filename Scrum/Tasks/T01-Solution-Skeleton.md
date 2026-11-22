# T01: Solution restructure and Clean Architecture skeleton

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** All · **Estimate:** 3 h · **Depends on:** none · **Status:** Done

## Goal
Turn the current single `HeroRegistry` Web API template into a Clean Architecture solution with separate source and test projects, and enforce the dependency rule.

## Steps
- [x] Move the existing project to `HeroRegistry/src/HeroRegistry.Api` and rename it. Delete `WeatherForecast.cs`.
- [x] Create the class libraries (`net10.0`):
  - `HeroRegistry.Domain`: no project or package references
  - `HeroRegistry.Application`: references Domain
  - `HeroRegistry.Infrastructure`: references Application and `Microsoft.Data.SqlClient`
  - `HeroRegistry.Api`: references Application and Infrastructure (Infrastructure is used for DI composition only)
- [x] Create the xUnit test projects under `HeroRegistry/tests/`:
  - `HeroRegistry.Domain.Tests`
  - `HeroRegistry.Application.Tests` (NSubstitute)
  - `HeroRegistry.Infrastructure.Tests` (Testcontainers.MsSql)
  - `HeroRegistry.Api.Tests` (Microsoft.AspNetCore.Mvc.Testing)
  - All of them use Shouldly and ~~coverlet.collector~~ Microsoft.Testing.Extensions.CodeCoverage (see notes).
- [x] Add `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true` (src only) and `AnalysisLevel=latest`.
- [x] Add `Directory.Packages.props` (Central Package Management).
- [x] Register every project in `HeroRegistry.slnx` using `src` and `tests` solution folders.
- [x] Add a `DependencyInjection.cs` extension in Application (`AddApplication()`) and in Infrastructure (`AddInfrastructure(IConfiguration)`).

## Tests (TDD)
- [x] Architecture test (in `Application.Tests`, using Reflection):
  - Domain references no other project and no third-party packages.
  - Application does not reference Infrastructure, Api or `Microsoft.Data.SqlClient`.
- [x] Verify that no project references `EntityFramework`, `Dapper` or `MediatR`.

## Done when
- [x] `dotnet build HeroRegistry/HeroRegistry.slnx` completes with 0 warnings (Debug and Release).
- [x] `dotnet test` runs, and the architecture tests pass (33 tests).

## Implementation notes
- **Build props layout:** shared settings live in `HeroRegistry/Directory.Build.props`. `src/Directory.Build.props` adds `TreatWarningsAsErrors`. `tests/Directory.Build.props` adds the shared test packages and global usings (`Xunit`, `Shouldly`).
- **Test platform:** xUnit v3 runs on Microsoft.Testing.Platform (MTP). The .NET 10 SDK no longer runs it through VSTest, so the repository-root `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`.
  - Run the tests with `dotnet test --solution HeroRegistry/HeroRegistry.slnx`.
  - `coverlet.collector` (a VSTest data collector) was replaced with `Microsoft.Testing.Extensions.CodeCoverage`: `--coverage --coverage-output-format cobertura`.
- **Architecture tests** check two levels:
  - The declared references in the `.csproj` and `.props` files. This is needed because the compiler drops assembly references that are not yet used.
  - The compiled assemblies, through `GetReferencedAssemblies()`. Each layer exposes an `AssemblyReference` marker for this.
- **Verified red:** temporarily adding `Microsoft.Data.SqlClient` to Domain and Application and declaring `Dapper` in `Directory.Packages.props` made 3 tests fail. The changes were then reverted.
- **Dockerfile:** updated for the new layout. The build context is the `HeroRegistry/` folder (`docker build -f src/HeroRegistry.Api/Dockerfile .`), and the image builds successfully.
- **Package versions:** ASP.NET Core and Extensions packages are pinned to `10.0.x` to match `net10.0`. The latest `11.0.0` versions target .NET 11.
