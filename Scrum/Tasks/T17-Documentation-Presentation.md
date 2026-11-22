# T17: Documentation, GenAI write-up and presentation

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Documentation · **Estimate:** 4 h · **Depends on:** all

## Goal
Make the project easy to set up, understand and present, and document how GenAI tools were used.

## Steps
- [ ] Final `README.md` review:
  - Setup for Aspire and for Docker Compose
  - Demo credentials
  - Architecture diagram and endpoint tables
  - Test commands and design decisions
- [ ] `docs/genai.md`, covering the GenAI exercise (a RESTful **task management** API: CRUD for tasks with `title`, `description`, `status` and `due_date`, where each task belongs to a user):
  - The exact prompt(s) used with the GenAI tool (Claude Code), and why they are structured that way (context, constraints, acceptance criteria, output format).
  - A representative sample of the generated code.
  - How the output was **validated**: compiled, ran tests, reviewed for security (authorization by owner, input validation, SQL injection), checked against the requirements.
  - What was **corrected or improved**: for example, missing ownership checks, status enum validation, past `due_date` handling, pagination and error responses.
  - How **edge cases, authentication and validation** were handled.
  - Examples of AI suggestions that were rejected in this project, and why (for example, an AI suggestion to use Dapper or EF, which this project does not allow).
- [ ] `docs/architecture.md` (optional): layer responsibilities, a request flow sequence (Controller, Service, Repository, Mapper, SQL Server) and the Reflection mapper internals.
- [ ] Presentation outline (`docs/presentation.md`):
  1. User story and personas
  2. Architecture and design choices
  3. Reflection data layer deep dive
  4. TDD and coverage
  5. Live demo (Aspire dashboard, then UI CRUD, then 401/403 scenarios)
  6. GenAI usage
  7. Q&A
- [ ] Verify that the Definition of Done in US-001 is fully checked.

## Done when
- A reviewer can clone the repository, follow the README and run the demo without help.
