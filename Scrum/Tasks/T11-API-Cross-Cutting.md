# T11: API cross-cutting concerns

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** API · **Estimate:** 3 h · **Depends on:** T01

## Goal
Add consistent error handling, documentation, CORS, health checks and configuration to the API.

## Steps
- [ ] Global exception handling with `IExceptionHandler` plus `AddProblemDetails()`:
  - Unhandled exceptions return 500 ProblemDetails with no stack trace outside Development.
  - `DuplicateEntityException` returns 409.
  - Include the `traceId` in responses.
- [ ] Customize `InvalidModelStateResponseFactory` so that model binding errors return `ValidationProblemDetails` in the same shape as Application validation errors.
- [ ] Add a JWT Bearer security scheme to the OpenAPI document and configure Scalar's preferred scheme (`AddPreferredSecuritySchemes`), so authorized endpoints can be called from Scalar. The OpenAPI document and Scalar UI themselves are done in [T02.1](T02.1-OpenAPI-Scalar.md).
- [ ] CORS policy `ui`: allowed origins come from configuration (`Cors:AllowedOrigins`), for the docker-compose UI origin and `http://localhost:4200`.
- [ ] Health check `/health`, including a SQL Server connectivity check. Aspire ServiceDefaults provides `/health` and `/alive`.
- [ ] Options pattern with validation for `JwtOptions`, `CorsOptions` and `DatabaseOptions`.
- [ ] JSON options: camelCase, enums as strings, and `DateOnly` support.

## Tests
- [ ] A test endpoint that throws returns 500 ProblemDetails without exception details.
- [ ] A malformed JSON body returns 400 ValidationProblemDetails.
- [ ] `/health` returns 200 anonymously.
- [ ] A CORS preflight from an allowed origin succeeds, and one from an unknown origin is rejected.

## Done when
- Every error response from the API follows the ProblemDetails format.
