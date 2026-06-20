# B-011: Wire frontend dwarf list and selected dwarf panel

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-011`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Frontend dwarf feature
Risk class: High
Recommended implementation model: Bounded integration tier (GPT-5.4 high)
Recommended reasoning level: High
Recommended review model: Architecture-sensitive review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

The browser loads the backend dwarf list, lets the player select one dwarf, fetches the matching snapshot, and clearly identifies that dwarf across accessible loading, empty, success, degraded, and error states.

## Why This Slice Exists

This feature establishes browser-owned selection and the player-visible identity used by later chat. It never reads or mirrors the unit highlighted in the Dwarf Fortress UI.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-011
- product/API sections and B-011 in `docs/specs/fortress-souls-v0.1.spec.md`
- completed B-007 frontend shell and B-009 dwarf API
- `.agents/instructions/frontend.instructions.md`
- `.agents/instructions/testing.instructions.md`

Conditional context:

- query-layer conventions established in B-007.

## Existing State To Inspect

The dwarf feature does not exist at planning time. After dependencies land, inspect the B-007 health API client, query/state owner, test utilities, CSS/accessibility conventions, and actual B-009 public dwarf response before adding feature files.

## In Scope

- Add typed API functions for `GET /api/dwarves` and `GET /api/dwarves/{dwarfId}/snapshot`.
- Implement list rendering and keyboard-accessible browser selection.
- Fetch and render the selected dwarf's identity and snapshot summary.
- Cover loading, empty list, no selection, snapshot loading, success, stale/degraded, backend unavailable, invalid/stale selected ID, and safe error states.
- Provide a development-only collapsible snapshot preview using only the published contract.
- Add user-facing component tests.

## Out Of Scope

- No chat, polling/background simulation unless explicitly approved, raw provider diagnostics, markdown renderer, persistent selection, or raw DFHack data.
- No duplicate API cache or client-side domain contract.

## Boundaries And Invariants

- Frontend consumes published DTOs only and never imports backend types.
- The browser is the only selection owner; DF UI cursor/highlight state is irrelevant.
- Snapshot/debug display is bounded, escaped, development-only where required, and never uses unsafe HTML.
- Errors omit stack traces, paths, raw payloads, and internal adapter details.

## Implementation Slices

### Slice 1: Dwarf list and browser selection

- Intended behavior: approved API state renders loading, empty, success, and error UI.
- Likely files or modules touched: `features/dwarves`, API client, query/state layer, tests.
- Test-first evidence: mocked API boundary tests fail for each state before implementation.
- Completion evidence: accessible identity/empty/error assertions pass.

### Slice 2: Selected snapshot summary and refresh

- Intended behavior: user can perform only the approved interaction and see the matching snapshot.
- Likely files or modules touched: dwarf details, optional selector/refresh, development panel.
- Test-first evidence: interaction test fails before data transition logic exists.
- Completion evidence: current identity and snapshot cannot become mismatched in tests.

## Acceptance Criteria

- [ ] Dwarf list loads from the backend with loading, empty, success, and safe error states.
- [ ] Keyboard-accessible browser selection fetches and clearly identifies the matching snapshot.
- [ ] Loading, empty, success, degraded, and safe error states are tested.
- [ ] Approved select/refresh behavior is keyboard accessible and keeps identity/snapshot consistent.
- [ ] Developer snapshot view is explicit, bounded, escaped, and contract-only.
- [ ] No raw backend/provider/DFHack error is displayed.

## Test Strategy

Write component behavior tests first using the real query/state wrapper and mocked HTTP boundary. Query by role/name/status, not implementation structure. Do not mock React internals or duplicate a browser E2E test here.

## Observability And Failure Behaviour

No browser telemetry is required. Show stable user messages and correlation ID when available. Distinguish no current dwarf from backend unavailable without exposing adapter internals.

## Validation

```powershell
Set-Location .\src\frontend
npm test -- --run
npm run lint
npm run typecheck
npm run build
Set-Location ..\..
.\scripts\test.ps1
```

Manually verify keyboard navigation and all approved states against fake mode.

## Stop Conditions

- Stop if UI needs data not present in the published contract.
- Stop before exposing raw snapshots in production or adding persistent browser state.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
