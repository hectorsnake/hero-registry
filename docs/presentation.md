# Presentation

The deck is [`presentation/HeroRegistry.pptx`](presentation/HeroRegistry.pptx), with a PDF copy in [`presentation/HeroRegistry.pdf`](presentation/HeroRegistry.pdf). It is generated from one slide list, so this outline and the speaker notes below always match the deck. Screenshots are in [`presentation/images`](presentation/images): taken from the running application, with no secrets or personal data.

**Format:** about 25 minutes. Roughly 3 for the story, 8 for architecture and decisions, 5 for quality and delivery, 3 for GenAI, then the live demo and questions. Slide 17 is a backup in case the demo cannot run.

## Outline

1. User story and personas
2. Architecture and design choices (including the decisions beyond the specification)
3. Reflection data layer deep dive
4. TDD and coverage
5. Live demo (Aspire dashboard, then UI CRUD, then 401/403 scenarios)
6. GenAI usage
7. Q&A

## Slides and speaker notes

### Introduction

**1. SuperHero Registry**

> Introduce the project in one sentence: a full-stack registry of superheroes, built test-first with Clean Architecture on .NET 10 and Angular 22.
>
> Say what the talk covers: the user story, the architecture, the decisions that go beyond the specification, quality and delivery, how GenAI was used, then a live demo.
>
> Mention that the repository, the README and the docs folder hold everything shown here.

**2. Agenda**

> Walk through the agenda quickly; it mirrors the order of the talk.
>
> Budget about 25 minutes: 3 for the story, 8 for architecture and decisions, 5 for quality and delivery, 3 for GenAI and the rest for the live demo and questions.

**3. The user story**

> Read the story aloud: it is the reason for every feature.
>
> Three personas drive authorization: visitors can only register or sign in, agents work with heroes, admins can also delete and manage users.
>
> The story has 12 acceptance criteria (Given/When/Then) and was split into 21 tasks, each with its own tests and Definition of Done. Everything is in the Scrum folder of the repository.

### Architecture and decisions

**4. Clean Architecture**

> Dependencies point inwards: Api and Infrastructure depend on Application, Application depends on Domain, and Domain depends on nothing.
>
> Use cases talk to ports (interfaces such as IHeroRepository and ITokenService); Infrastructure implements them with ADO.NET, BCrypt and JWT. The Api project is the composition root.
>
> The rule is not just a convention: architecture tests fail the build if the Domain gains a project reference, or if anyone references Entity Framework, Dapper or MediatR.
>
> Expected failures travel as Result<T> with a typed Error, and the API turns them into RFC 9457 problems in one place.

**5. Beyond the spec: data and rules**

> The specification sets the frame; these are the decisions I made inside it.
>
> Data access: the specification forbids Entity Framework, Dapper and Mediator. Instead of hand-writing a mapping method per entity, I wrote a small ADO.NET data layer with a Reflection mapper: fluent maps, metadata cached per type, parameterized SQL only.
>
> Schema: three tables instead of the minimum two, with soft delete and audit columns, created by versioned SQL scripts that the API applies at startup with checksums.
>
> Business rules: duplicate hero names and emails are a 409, every field has rules that mirror on the client, and all field errors come back at once.
>
> API: search, sort and paging on the list, 201 with a Location header, and one error format.

**6. Beyond the spec: security and platform**

> Users and login: the specification asks for user creation, login and authorized endpoints. I added BCrypt hashing, a 15-minute JWT kept in memory, a rotating refresh token in an HttpOnly cookie with reuse detection, an Admin role, and account updates where users can only change themselves.
>
> Front end: Angular 22, zoneless and signals-first, with Signal Forms that mirror the server rules, silent token refresh, accessibility and a layout that works at 360 px.
>
> Delivery: Aspire for development, Docker Compose for one-command delivery, and a GitHub Actions pipeline that also smoke-tests the Compose stack.
>
> Operations was not required at all: OpenTelemetry traces down to each SQL statement, health checks, and Application Insights, without personal data in logs.

**7. The Reflection data layer**

> Each entity has a small fluent map; this is the whole configuration for superheroes. Audit columns, the key and soft delete come from the AuditableEntityMap base class.
>
> At first use, EntityMetadata reads the map with Reflection, validates every identifier, and caches the result per type, so Reflection is paid once.
>
> SqlStatementBuilder generates SELECT, INSERT with OUTPUT INSERTED.Id, UPDATE and soft-delete statements, with one parameter per value: no string concatenation of values, ever.
>
> DataRecordMapper resolves column ordinals once per result set and materializes entities through their private constructor and setters, so the domain keeps its encapsulation.
>
> Unique index violations, SQL errors 2601 and 2627, become a DuplicateEntityException naming the field, which the use case turns into a 409.

### Building it

**8. Heroes CRUD**

> Five endpoints cover the CRUD. Every one needs a signed-in user; deleting is for admins only.
>
> The list takes search, sort and paging from the query string; the UI keeps them in the URL so a list can be shared and the back button works.
>
> Creating returns 201 with a Location header. Validation problems are a 400 with every field error; duplicates are a 409 naming the field; deleting is a soft delete that keeps the row for the audit trail.
>
> The screenshot is the list as an admin: chips with a +N tooltip, a power meter, and View, Edit and Delete actions with accessible names.

**9. Business rules, enforced on both sides**

> The rules live in the Application and Domain layers, independent of the API and the database; the Angular schemas mirror them so most mistakes are caught before a request.
>
> Uniqueness is the interesting one: the use case checks hero name and email before writing, and a unique index in SQL Server is the final guard for races, mapped back to the same 409.
>
> The screenshot is a real 409: saving a hero named Superman. In the Aspire trace of that request you can see the two duplicate-check SELECTs and no INSERT.
>
> Audit columns are set by the server only, deletes are soft, and account updates follow AC11: users change only their own account, admins any account.

**10. A secure session: rotating refresh tokens**

> Sign-in verifies the password with BCrypt; unknown users still get a dummy verification, so timing does not reveal which user names exist.
>
> The API returns a 15-minute JWT, which the UI keeps in memory only, and sets the refresh token as an HttpOnly, Secure, SameSite=Strict cookie limited to /api/auth. JavaScript can never read it.
>
> When a request gets a 401, one shared refresh renews both tokens and the request is retried once; the user never sees it.
>
> Every refresh rotates the token. If an old token is replayed more than 30 seconds after rotation, it is treated as stolen and the whole family is revoked. Two tabs refreshing at once get a refresh_race and simply retry.
>
> Only SHA-256 hashes are stored. Logout revokes the family and signs out the other tabs through a BroadcastChannel.

**11. Authorization: 401 and 403**

> Every endpoint requires a signed-in user unless it is explicitly anonymous; that is a fallback policy, so a new endpoint is secure by default.
>
> At the top are two real responses from the API: an anonymous request gets a 401 with WWW-Authenticate: Bearer, and a regular user deleting a hero gets a 403. Both are RFC 9457 problems with a stable code and a trace id.
>
> In the UI, a 401 first triggers the silent refresh. Only if that fails is the user sent to the login page, with a returnUrl and an explanation (see the backup slide); a 403 shows a toast.
>
> The UI also hides what a user cannot do: regular users never see Delete.

**12. The Angular front end**

> Angular 22 without zone.js: OnPush components and signals everywhere. Reads use httpResource, so changing the search or the page refetches and cancels the previous request automatically.
>
> The URL is the source of truth for search, sort and page.
>
> Forms use the new Signal Forms. Their schemas mirror the server rules, and 400 or 409 errors from the server land next to the right field.
>
> Accessibility: every input has a label, errors are announced, focus moves to the first invalid field and back to the list after a delete.
>
> The layout is mobile-first; at 360 px the table becomes cards with the same actions.

### Quality and delivery

**13. Test-driven, and measured**

> Every unit was driven by a failing test first, and the red runs are recorded in each task file as evidence.
>
> 1,116 automated tests: 763 on the back end, including integration tests against a real SQL Server started by Testcontainers, and 353 on the Angular side.
>
> The chart shows line and branch coverage per layer, measured from the CI-style run. Domain and Application, where the business rules live, are near 99 percent; the Definition of Done asked for 80.
>
> The Api project is the lowest at 78 percent of lines; the endpoints themselves are covered end to end by the API tests.

**14. Run it anywhere: Aspire, Docker and CI**

> For development, one command starts SQL Server, the API and the UI under .NET Aspire. The dashboard shows every resource and the traces; the middle picture is one request traced down to its SQL statements.
>
> For delivery, docker compose up --build runs SQL Server, the API in a non-root image and nginx serving the UI and proxying the API, each waiting for the previous one to be healthy. The API migrates and seeds the database itself.
>
> GitHub Actions builds and tests the back end and the front end in parallel, then builds the images and smoke-tests the whole stack. The bottom picture is a green run.

### GenAI

**15. GenAI: prompt precisely, verify everything**

> The tool was Claude Code. Every task started from a written plan with acceptance criteria, so the AI worked against something checkable.
>
> This project is my answer to the exercise's GenAI section: instead of imagining an AI-generated REST API, I generated this one, task by task.
>
> The left side is the prompt anatomy. The shared context is written once, as files: the exercise, the user story with its acceptance criteria, project skills and standing constraints. Each task file adds the goal, the steps with their edge cases, the tests and a Done-when list. The prompt itself is then one short instruction, and the tool records its red runs and measured numbers back in the task file.
>
> The right side is the important part: real mistakes in AI-generated code that unit tests did not catch, found by a real browser, a real CI runner or a user, then fixed test-first. That is the critical thinking the exercise asks for.
>
> Everything is written up in docs/genai.md, including the suggestions I ruled out.

### Demo and Q&A

**16. Live demo, then questions**

> Demo script, about 6 minutes:
>
> 1. Aspire dashboard: resources, then a trace of one request down to SQL.
>
> 2. Sign in as Admin; search for "flight", sort by power, page.
>
> 3. Create a hero: show validation, then a duplicate name for the 409.
>
> 4. Edit and save; press F5 and show the session survives.
>
> 5. Delete as admin with the confirmation dialog.
>
> 6. Sign in as a regular user: no Delete; show the 403 from the API.
>
> 7. Log out in one tab: the other tab follows.
>
> If Docker or the network fails, switch to the backup slide with screenshots.
>
> Then open the floor for questions; the repository URL is on the slide.

### Backup

**17. Backup: the demo in screenshots**

> Use this slide if the live demo cannot run.
>
> Top left: the list as an admin, with search, sort and actions. Top right: the delete confirmation, with Cancel focused.
>
> Bottom left: the form after an invalid submit, every error next to its field. Bottom right: the login page after a failed silent refresh, with the reason and the returnUrl.
