# T17: Documentation, GenAI write-up and presentation

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Documentation · **Estimate:** 6 h · **Depends on:** all

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
- [ ] PowerPoint presentation (`docs/presentation/HeroRegistry.pptx`), built from the outline above:
  - About 10–14 slides for a 20–30 minute talk: title, agenda, one or more slides per outline section, and a closing slide with the repository URL.
  - Visuals over text: the architecture diagram, the request flow, a Reflection mapper code snippet, test and coverage results (screenshots of the test run and coverage report), the CI pipeline run, and screenshots of the UI (hero list, form validation, 401/403 handling) and of the Aspire dashboard.
  - Speaker notes on every slide with the key talking points.
  - A backup slide with screenshots of the live demo, in case Docker or the network fails during the presentation.
  - No secrets, real connection strings or personal data in the slides or screenshots.
  - Export a PDF copy (`docs/presentation/HeroRegistry.pdf`) for reviewers without PowerPoint. The PDF ignore rule in `Scrum/.gitignore` only applies to `Scrum/`, so this file is committed.
  - Rehearse the talk once with a timer, including the live demo.
- [ ] Verify that the Definition of Done in US-001 is fully checked.

## Done when
- A reviewer can clone the repository, follow the README and run the demo without help.
- `HeroRegistry.pptx` and its PDF copy are in `docs/presentation/`, follow the outline, and the talk fits the time slot.
