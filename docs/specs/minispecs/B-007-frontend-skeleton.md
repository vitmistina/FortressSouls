# B-007: Create frontend skeleton

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-007`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Frontend application shell
Risk class: Medium
Recommended implementation model: Bounded implementation tier (GPT-5.4)
Recommended reasoning level: Medium
Recommended review model: Bounded review tier (GPT-5.4 high)
Human checkpoint: No

## Observable Outcome

The React/Vite app builds, tests run, and the browser shows an accessible shell with backend health plus placeholders for the dwarf list, browser-selected dwarf, and chat.

## Why This Slice Exists

The shell establishes the frontend build, API boundary, and state conventions before feature UI. Completing it alongside the backend and observability shells lets B-006 create a useful local development loop before feature work starts.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-007
- `docs/specs/fortress-souls-v0.1.spec.md`, especially product slice and health contract
- `docs/backlog/v0.1-backlog.md`
- `docs/decisions/adr-0001-stack.md`
- `.agents/instructions/frontend.instructions.md`
- `.agents/instructions/testing.instructions.md`
- implemented B-004 health contract

Conditional context:

- None.

## Existing State To Inspect

At planning time `src/frontend/` and a package-manager lockfile do not exist. Recheck both before scaffolding and preserve any frontend conventions added since this plan.

## In Scope

- Scaffold React, TypeScript, Vite, CSS, and the selected package-manager lockfile.
- Add an app shell, a typed health API client, and explicit loading/success/error states.
- Add semantic landmarks, page title, visible focus, and accessible status messaging.
- Add placeholders for the backend dwarf list, browser-selected dwarf panel, and chat workflow without implementing those features.
- Add component/API-boundary tests and package scripts for lint/type-check/test/build.

## Out Of Scope

- No dwarf data integration, chat behavior, provider diagnostics, complex styling, UI framework, or duplicate server cache.
- No dwarf list/snapshot API integration or selection behavior until B-011.

## Boundaries And Invariants

- Frontend depends only on published HTTP contracts.
- Browser input and server responses are untrusted and receive typed runtime-safe handling at the API boundary.
- Raw errors, paths, secrets, stack traces, prompts, and provider details are not shown.
- Server state has one owner; introduce TanStack Query only if the resulting health/data flow needs it.

## Implementation Slices

### Slice 1: Build and test shell

- Intended behavior: app renders semantic static structure and package checks pass.
- Likely files or modules touched: `src/frontend` manifests, Vite/TypeScript config, `src/app`, styles, component test setup.
- Test-first evidence: rendering test fails before the app shell exists.
- Completion evidence: lint, type-check, test, and build pass.

### Slice 2: Health state

- Intended behavior: health request shows loading, success, and safe error states.
- Likely files or modules touched: API client and diagnostics feature.
- Test-first evidence: mocked-boundary tests for each state fail before implementation.
- Completion evidence: tests pass and manual browser check reaches the real B-004 endpoint.

## Acceptance Criteria

- [ ] Frontend builds and has committed package-manager metadata/lockfile.
- [ ] Health loading, success, and safe error states are tested.
- [ ] Shell placeholders clearly reserve the backend-list/browser-selection workflow.
- [ ] Controls and status output are keyboard/screen-reader accessible.
- [ ] No later feature behavior or unnecessary dependency is introduced.

## Test Strategy

Write the render test and health-state component tests first. Mock only the HTTP boundary and use user-facing queries. Do not assert private component structure or add a browser E2E test in this slice.

## Observability And Failure Behaviour

No browser telemetry is added. Health failures show a stable user message and optional correlation ID when supplied by the API; raw network/provider errors remain hidden.

## Validation

Focused development checks (use scripts actually declared in `package.json`):

```powershell
Set-Location .\src\frontend
npm test -- --run
```

Final relevant checks:

```powershell
Set-Location .\src\frontend
npm run lint
npm run typecheck
npm test -- --run
npm run build
```

Manual verification: run backend and frontend, confirm health loading/success/error states and keyboard focus in the browser.

## Stop Conditions

- Stop if an existing lockfile selects a package manager other than npm.
- Stop before adding a component library or state dependency without demonstrated need.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
