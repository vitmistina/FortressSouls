# B-015: Build frontend chat panel with fake provider backend

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-015`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Frontend chat feature
Risk class: High
Recommended implementation model: Bounded integration tier (GPT-5.4 high)
Recommended reasoning level: High
Recommended review model: Architecture-sensitive review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

The player can send a bounded message to the clearly identified browser-selected dwarf, see ordered in-memory turns and safe progress/errors, and optionally inspect the development prompt preview.

## Why This Slice Exists

This completes the fake-mode user loop. Selecting a different dwarf creates or explicitly resets to a new session so conversation history cannot cross dwarf identities.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-015
- user journey/API sections and B-015 in `docs/specs/fortress-souls-v0.1.spec.md`
- completed B-011 and B-014 public behavior
- `.agents/instructions/frontend.instructions.md`
- `.agents/instructions/testing.instructions.md`

Conditional context:

- frontend query/state conventions and development-mode configuration established by B-007/B-011.

## Existing State To Inspect

No frontend exists at planning time. After dependencies, inspect current-dwarf state ownership, API clients, component/test conventions, and exact chat DTO/error contract before designing component state.

## In Scope

- Add typed chat API functions and feature components for session creation, input, ordered messages, pending state, safe errors, and development preview.
- Bind the session to the browser-selected dwarf ID and start/reset the session explicitly when selection changes.
- Bound input length, prevent duplicate submits while a turn is pending, preserve draft text on recoverable failure, and keep controls keyboard accessible.
- Render text safely as plain text; show provider/model/latency diagnostics only from approved DTO fields.
- Add component behavior tests for create/send/success/error/reset or refresh behavior.

## Out Of Scope

- No persistence, streaming, markdown/HTML rendering, optimistic assistant prose, retries, tool UI, model picker, multi-session navigation, or raw prompt in production.

## Boundaries And Invariants

- Server session/history is authoritative; frontend does not invent provider results.
- A session can never silently continue under a different dwarf identity.
- Conversation and prompt content never enter browser telemetry or error reporting.
- Prompt preview is development-only, escaped, and explicitly opened.
- Error UI exposes stable messages/correlation IDs, not internal/provider details.

## Implementation Slices

### Slice 1: Session and send flow

- Intended behavior: the browser-selected dwarf creates/uses a bound session and one message produces one rendered fake reply.
- Likely files or modules touched: `features/chat`, chat API client, state/query integration, tests.
- Test-first evidence: user-level create/send test fails first.
- Completion evidence: ordered messages, pending control state, and cleared input assertions pass.

### Slice 2: Identity transitions and failures

- Intended behavior: approved refresh/selection transition resets or rebinds explicitly and errors preserve a coherent UI.
- Likely files or modules touched: chat/browser-selection integration and tests.
- Test-first evidence: identity mismatch, duplicate submit, provider failure, and backend unavailable tests fail first.
- Completion evidence: no history crosses identities and recovery is usable.

### Slice 3: Development preview

- Intended behavior: explicit development control fetches and safely displays preview; production has no control.
- Likely files or modules touched: prompt debug component/config tests.
- Test-first evidence: visibility/escaping tests fail first.
- Completion evidence: development/production behavior passes.

## Acceptance Criteria

- [ ] Selecting a listed dwarf creates or uses a session bound to that ID; switching selection starts/resets without mixed history.
- [ ] Player can send and receive fake-mode messages with accessible pending state.
- [ ] Input/duplicate submission is bounded and recoverable failures preserve coherent state.
- [ ] Dwarf identity changes cannot mix conversations.
- [ ] Preview is explicit, escaped, and absent outside Development.
- [ ] Frontend tests cover success, errors, pending, identity transition, and keyboard submission.

## Test Strategy

Write user-facing component tests first with a mocked HTTP boundary and real chat state/query wrapper. Use roles/labels/text and controlled promises, never sleeps. Do not mock child components or assert internal hook calls.

## Observability And Failure Behaviour

No browser content telemetry. Surface stable messages for backend unavailable, invalid session, no selected/listed dwarf, prompt too large, provider failure, timeout, and cancellation when represented publicly. Show correlation ID where supplied.

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

Manually complete the fake chat journey with keyboard only and verify identity transition plus Development preview.

## Stop Conditions

- Stop if required state is missing from published API DTOs.
- Stop before adding persistence, streaming, markdown execution, or a new state framework.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
