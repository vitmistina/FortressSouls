# B-022: End-to-end fake-mode browser smoke test

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-022`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: End-to-end tests
Risk class: High
Recommended implementation model: Cross-module test tier (GPT-5.4 high)
Recommended reasoning level: High
Recommended review model: Release-path review tier (GPT-5.5 high)
Human checkpoint: No

## Observable Outcome

One deterministic browser smoke test starts fake mode, loads the dwarf list, selects one dwarf, sends a message, observes a fake reply and safe diagnostics, and requires no secrets, network provider, Dwarf Fortress, or persisted state.

## Why This Slice Exists

Unit/component coverage cannot prove the critical browser-owned selection and chat journey is wired end to end.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-022
- testing/journey sections and B-022 in `docs/specs/fortress-souls-v0.1.spec.md`
- completed B-015 fake-mode UI and B-006 scripts
- `.agents/instructions/testing.instructions.md`
- `.agents/instructions/frontend.instructions.md`

Conditional context:

- existing frontend test runner/browser tooling and CI configuration; reuse rather than add a second framework.

## Existing State To Inspect

At planning time no app or E2E framework exists. After dependencies, inspect committed frontend test dependencies, local start scripts, stable user-facing labels, port/config support, and CI environment before selecting the narrowest browser runner.

## In Scope

- Add exactly one critical fake-mode browser journey using the repository's existing browser test capability; add one minimal dependency only if no browser runner exists and human/project dependency policy allows it.
- Start backend/frontend in isolated fake mode with controlled ports and readiness checks, not sleeps.
- Load the list, select one dwarf in the browser, send one bounded message, and assert selected identity, fake response, and approved provider/adapter diagnostics.
- Prove no credential/DFHack requirement and clean up owned processes reliably.
- Wire the test into the canonical test/check path at an appropriate stage and document focused execution.

## Out Of Scope

- No live DFHack/provider, exhaustive UI permutations, screenshot golden suite, visual styling assertions, persistence, streaming, API-only substitute, or retries masking flakes.

## Boundaries And Invariants

- Test exercises real browser, HTTP, application, fake adapter, prompt assembler, fake provider, and in-memory session.
- External network is disabled/unneeded; fake mode is explicit.
- Locators are semantic/user-facing and deterministic.
- Readiness uses health polling with bounded timeout; no arbitrary sleeps.
- Process/temporary-resource cleanup runs on success and failure.

## Implementation Slices

### Slice 1: Deterministic test harness

- Intended behavior: isolated app starts, becomes ready, and stops cleanly.
- Likely files or modules touched: E2E config/harness, scripts, test environment config.
- Test-first evidence: initial browser smoke fails at missing harness/readiness.
- Completion evidence: repeated local runs start/stop without leaked processes/ports.

### Slice 2: Critical chat journey

- Intended behavior: user selects a listed dwarf, sends a message, and sees the matching reply/diagnostics.
- Likely files or modules touched: one E2E spec and canonical test script.
- Test-first evidence: journey assertion fails before final wiring/test exists.
- Completion evidence: repeated headless runs pass without network/secrets/DF.

## Acceptance Criteria

- [ ] One browser test covers list -> browser selection -> selected snapshot -> chat send -> fake reply -> safe diagnostics.
- [ ] Test uses fake adapter/provider and no secret, public network, or Dwarf Fortress.
- [ ] Harness uses bounded readiness and reliable cleanup without sleeps/retries.
- [ ] Canonical focused and full commands are documented and CI-suitable.
- [ ] Test passes repeatedly and fails when the protected journey is deliberately broken.

## Test Strategy

The smoke test itself is the behavior-first evidence. First run it red against a deliberately missing/unwired journey, then make only necessary harness/wiring changes. Do not mock browser HTTP or backend modules. Keep lower-level failure permutations in existing component/integration suites.

## Observability And Failure Behaviour

Assert only approved diagnostics visible to the user and optionally retain local trace/test artifacts without conversation/prompt content. On failure, capture bounded app logs/screenshots according to existing tooling and ensure secrets/content are redacted.

## Validation

Use the exact script introduced for the browser test, then:

```powershell
.\scripts\test.ps1
.\scripts\check.ps1
```

Run the focused browser test at least twice from a clean stopped state and record both results.

## Stop Conditions

- Stop if adding a new browser dependency is materially necessary without human approval.
- Stop if determinism requires retries, arbitrary sleeps, live services, or shared developer ports/state.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
