# B-004: Create backend solution skeleton

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-004`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Backend solution and API composition
Risk class: Medium
Recommended implementation model: Bounded implementation tier (GPT-5.4)
Recommended reasoning level: High
Recommended review model: Architecture-sensitive review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

The .NET solution builds, its test suite runs, and a locally started API returns the documented basic response from `GET /api/health`.

## Why This Slice Exists

This establishes the inward dependency direction and the executable backend shell on which all later modules depend. It must create useful boundaries without implementing dwarf, prompt, chat, provider, or telemetry behavior early.

## Context To Load

Required context:

- `AGENTS.md`
- this mini-spec and parent backlog row
- `docs/architecture/0001-architecture-overview.md`
- `docs/decisions/adr-0001-stack.md`
- `docs/decisions/adr-0002-modular-monolith.md`
- `docs/specs/fortress-souls-v0.1.spec.md`, sections 7, 8.1, and B-004
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/skills/modular-monolith-boundaries/SKILL.md`

Conditional context:

- `docs/decisions/adr-0004-observability.md` only if health response wiring would pre-empt B-005.

## Existing State To Inspect

At mini-spec creation, `src/backend/` does not exist. Recheck the tree and `git status` before editing. Inspect any solution, central build properties, SDK pin, or tests introduced since this plan before choosing project names or references.

## In Scope

- Create `src/backend/FortressSouls.sln` and projects for API, Application, Domain, DwarfFortress, Llm, Prompting, Observability, and tests. Keep runtime fakes in their owning adapter modules and test-only helpers in the test project rather than adding a separate TestDoubles assembly.
- Target .NET 10, enable nullable reference types and implicit usings, and treat new compiler/analyzer warnings as failures where the established SDK supports it.
- Add only inward project references allowed by ADR-0002.
- Implement the minimal health endpoint with `status`, version, adapter, and provider fields; adapter/provider may report `NotConfigured` until their backlog items exist.
- Add a test that starts the API in memory and verifies the health contract.
- Add an architecture test or an equally automated project-reference assertion for forbidden Domain dependencies.

## Out Of Scope

- No OpenTelemetry packages or custom telemetry behavior from B-005.
- No local orchestration, frontend, dwarf contracts, adapters, prompting, providers, or chat.
- No shared utilities project, database, service split, or speculative interfaces.

## Boundaries And Invariants

- Domain has no ASP.NET Core, DFHack, provider, telemetry, filesystem, or process dependency.
- Application depends inward on Domain; adapters and API composition depend on application-owned contracts.
- API endpoints remain transport-only.
- The health endpoint returns no secrets, environment values, internal paths, or exception details.
- No endpoint or interface can execute DFHack or arbitrary commands.

## Implementation Slices

### Slice 1: Compiling modular solution

- Intended behavior: the solution and projects restore and compile with the accepted reference graph.
- Likely files or modules touched: `src/backend/FortressSouls.sln`, project files, minimal source placeholders, shared build configuration if justified.
- Test-first evidence: add the smallest architecture/reference test that fails when Domain references an outer project.
- Completion evidence: solution build and architecture test pass.

### Slice 2: Executable health contract

- Intended behavior: the API starts and returns the basic v0.1 health response.
- Likely files or modules touched: API startup/endpoint files and API integration tests.
- Test-first evidence: write the in-memory `GET /api/health` integration test before endpoint implementation and observe the expected failure.
- Completion evidence: the integration test and full backend suite pass.

## Acceptance Criteria

- [ ] `FortressSouls.sln` builds on the documented .NET 10 SDK.
- [ ] Every planned backend project is included and has only allowed references.
- [ ] `GET /api/health` returns HTTP 200 and the documented non-sensitive fields.
- [ ] The API starts without DFHack, provider credentials, or telemetry infrastructure.
- [ ] At least one API integration test and one dependency-boundary check pass.
- [ ] No feature behavior from later backlog items is implemented.

## Test Strategy

Write the health integration test first, then the boundary assertion. Use the real ASP.NET Core test host and real JSON serialization; do not mock the endpoint pipeline. Keep pure project-reference checks deterministic and avoid tests that merely assert private class names.

## Observability And Failure Behaviour

B-004 introduces no custom telemetry. Startup failures remain normal framework failures for developers. Health must not report stack traces or configuration values; correlation middleware is deferred to B-005.

## Validation

Focused development checks:

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "Health|Architecture"
```

Final relevant checks:

```powershell
dotnet format .\src\backend\FortressSouls.sln --verify-no-changes
dotnet build .\src\backend\FortressSouls.sln
dotnet test .\src\backend\FortressSouls.sln --no-build
```

Manual verification:

```powershell
dotnet run --project .\src\backend\FortressSouls.Api\FortressSouls.Api.csproj
```

Call `GET /api/health` and record the status and response shape.

## Stop Conditions

- Stop if the installed SDK cannot target .NET 10.
- Stop if the proposed project graph requires changing ADR-0002.
- Stop before adding a new production dependency beyond the framework and test infrastructure needed for this skeleton.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
