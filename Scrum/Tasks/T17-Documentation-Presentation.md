# T17: Documentation, GenAI write-up and presentation

**User story:** [US-001](../US-001-SuperHero-Registry.md) · **Layer:** Documentation · **Estimate:** 6 h · **Depends on:** all · **Status:** Done (rehearsal left to the presenter)

## Goal
Make the project easy to set up, understand and present, and document how GenAI tools were used.

## Steps
- [x] Final `README.md` review:
  - Setup for Aspire and for Docker Compose
  - Demo credentials
  - Architecture diagram and endpoint tables
  - Test commands and design decisions
- [x] `docs/genai.md`, covering the GenAI exercise. The exercise asks you to imagine generating a RESTful API with a GenAI tool; this project (the SuperHero Registry API, generated with Claude Code) is the answer:
  - The prompts used with the GenAI tool (Claude Code), and why they are structured that way (shared context, task files with acceptance criteria and "Done when", short instructions).
  - A representative sample of the generated code.
  - How the output was **validated**: compiled, ran tests, reviewed for security (authorization, input validation, SQL injection), checked against the requirements.
  - What was **corrected or improved**: for example, duplicate checks, races, the order of checks and error responses.
  - How **edge cases, authentication and validation** were handled.
  - Examples of AI suggestions that were rejected in this project, and why (for example, an AI suggestion to use Dapper or EF, which this project does not allow).
- [x] `docs/architecture.md` (optional): layer responsibilities, a request flow sequence (Controller, Service, Repository, Mapper, SQL Server) and the Reflection mapper internals.
- [x] Presentation outline (`docs/presentation.md`):
  1. User story and personas
  2. Architecture and design choices
  3. Reflection data layer deep dive
  4. TDD and coverage
  5. Live demo (Aspire dashboard, then UI CRUD, then 401/403 scenarios)
  6. GenAI usage
  7. Q&A
- [x] PowerPoint presentation (`docs/presentation/HeroRegistry.pptx`), built from the outline above:
  - About 10–14 slides for a 20–30 minute talk: title, agenda, one or more slides per outline section, and a closing slide with the repository URL.
  - Visuals over text: the architecture diagram, the request flow, a Reflection mapper code snippet, test and coverage results (screenshots of the test run and coverage report), the CI pipeline run, and screenshots of the UI (hero list, form validation, 401/403 handling) and of the Aspire dashboard.
  - Speaker notes on every slide with the key talking points.
  - A backup slide with screenshots of the live demo, in case Docker or the network fails during the presentation.
  - No secrets, real connection strings or personal data in the slides or screenshots.
  - Export a PDF copy (`docs/presentation/HeroRegistry.pdf`) for reviewers without PowerPoint. The PDF ignore rule in `Scrum/.gitignore` only applies to `Scrum/`, so this file is committed.
  - Rehearse the talk once with a timer, including the live demo.
- [x] Verify that the Definition of Done in US-001 is fully checked.

## Implementation notes

### Deliverables
| File | Notes |
| ---- | ----- |
| `README.md` | Reviewed: setup (Aspire, Docker Compose), demo credentials, architecture, endpoints, tests and design decisions were already in place. Added: an accurate status line, the current test and coverage numbers, and a "Documentation and presentation" index |
| `docs/architecture.md` | Layers and the dependency rule; a request end to end (sequence diagram); the Reflection data layer internals; migrations; the session flow (sequence diagram); errors; observability; front end; delivery. Mermaid diagrams render on GitHub. Every technical claim was checked against the code (for example the identifier regex, the application lock and the edited-script check) |
| `docs/genai.md` | The GenAI exercise answered with this project: the prompts (user story, task files, skills, a short instruction) and why; representative generated code (`HeroService`); validation, including a security checklist; edge cases; a table of real AI mistakes caught by verification (from the task notes); and the options ruled out. No separate sample project: the registry itself is the answer |
| `docs/presentation.md` | The outline and the speaker notes of all 17 slides, generated from the same slide list as the deck, so they cannot drift |
| `docs/presentation/HeroRegistry.pptx` | 17 slides: 16 for the talk and 1 backup. Built as a structured deck (theme, two layouts, sections, speaker notes on every slide, a native coverage chart). Passes the OOXML validator |
| `docs/presentation/HeroRegistry.pdf` | Exported by PowerPoint itself (COM automation), so it matches what PowerPoint renders |
| `docs/presentation/images/` | 10 screenshots of the running system (Aspire dashboard and a trace, the hero list, form validation, the duplicate-name 409, the delete dialog, mobile at 375 px, the session-expired login, a green CI run, and the 401/403 responses), cropped; no secrets or personal data. GitHub's account banner was cropped out |

### Decisions beyond the specification (the deck's slides 5 and 6)
The specification was compared line by line with what was built:
- **Data:** ADO.NET with a Reflection mapper instead of the forbidden libraries; three tables with soft delete and audit; checksummed migrations.
- **Business rules:** duplicate checks, the password policy, the field rules.
- **API:** search, sort and paging; RFC 9457 errors.
- **Security:** BCrypt, a short JWT and rotating refresh tokens with reuse detection, roles, account rules.
- **Front end:** signals and Signal Forms.
- **Delivery:** Aspire, Docker Compose and CI.
- **Operations:** OpenTelemetry. None of it was required.

### Numbers used in the deck and README (measured, not estimated)
- **Tests:** 1,116 automated tests (763 back end from the CI-style run, 353 front end).
- **Back-end coverage:** the Cobertura reports of every test project were merged by layer.
  - Lines: Domain 98.3%, Application 98.8%, Infrastructure 95.4%, ServiceDefaults 98.9%, **Api 77.9%** (branches 60.9%). The Api is shown as it is, not rounded up.
- **Front end:** 97.9% lines and 93.4% branches.

### QA of the deck
- **Validator:** passes the skill's OOXML validator.
- **Rendering:** every slide was exported by PowerPoint and inspected. The first render found these issues, all fixed and re-rendered:
  - centered titles clashing with left-aligned content;
  - a two-line title crowding a table;
  - empty space under the comparison tables;
  - an unreadable 401/403 screenshot, replaced by code panels with the real response bodies;
  - an invisible icon (teal on teal);
  - chart labels rounded to integers;
  - small screenshots on the delivery and backup slides.
- **Content checks:**
  - no company name in the docs, README, deck text or PDF;
  - no placeholder text;
  - speaker notes on all 17 slides.

### Left to the presenter
- **Rehearse once with a timer,** including the live demo (about 25 minutes; the time budget is in `docs/presentation.md`). This cannot be done for you.
- **The Definition of Done item "verified manually through `HeroRegistry.Api.http`"** is still open (see US-001): one pass through the `.http` file, including AC11, which has no UI page.

## Done when
- A reviewer can clone the repository, follow the README and run the demo without help.
- `HeroRegistry.pptx` and its PDF copy are in `docs/presentation/`, follow the outline, and the talk fits the time slot.
