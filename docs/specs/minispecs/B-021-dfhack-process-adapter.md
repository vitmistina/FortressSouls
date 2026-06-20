# B-021: Implement DFHack process adapter

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-021`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: DwarfFortress process adapter
Risk class: Critical
Recommended implementation model: Process-safety implementation tier (GPT-5.5)
Recommended reasoning level: High
Recommended review model: Critical safety review tier (GPT-5.5 xhigh)
Human checkpoint: Required

## Observable Outcome

After human safety review, the backend can invoke only the approved read-only DFHack scripts, list eligible dwarves, fetch a snapshot by validated dwarf ID, and classify all researched process failures without exposing a generic command surface.

## Why This Slice Exists

This is the live game trust boundary and highest-risk implementation item. ADR-0003 fixes the allowlisted list/by-ID process strategy: the browser selects from returned list data, while the adapter remains independent of the unit highlighted in the DF UI.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-021
- `docs/decisions/adr-0003-dfhack-adapter.md`
- `docs/research/dfhack-command-invocation.md`
- `docs/research/dfhack-field-map.md`
- `docs/runbooks/dfhack-b019-manual-validation.md`
- completed B-008 contracts, JSON mapper, and error contract
- `dfhack/scripts/fortress-souls/diagnose.lua`, `list-dwarves.lua`, and `get-dwarf-snapshot.lua`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/dfhack.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/instructions/observability.instructions.md`
- `.agents/skills/dfhack-adapter-safety/SKILL.md`
- `.agents/skills/observability-first/SKILL.md`

Conditional context:

- current DFHack setup/install runbook when it exists; do not infer installation behavior from machine-local paths.

## Existing State To Inspect

Read-only Lua scripts, samples, research, manual validation, and exact ADR classifications exist; no backend runner exists. Re-audit scripts for mutation and verify the closed allowlist before editing application code.

## In Scope

- Define a closed enum/value allowlist mapping internally to exact approved command names; no raw command string crosses the runner boundary.
- Validate configured full `dfhack-run` path, working directory, loopback host/port, timeout, and output limits without exposing them publicly.
- Perform bounded cancellable TCP preflight, then launch the fixed executable with structured argument list and no shell.
- Concurrently drain bounded stdout/stderr, enforce timeout/cancellation, terminate the owned process tree safely, and classify start/timeout/cancel/negative/nonzero/oversize/invalid JSON failures exactly.
- Parse success output through the B-008 DTO/validation/mapping seam and implement list plus validated by-ID snapshot operations.
- Expose a bounded application-owned adapter readiness/last-outcome projection; reading status must not perform TCP preflight or launch a process.
- Expose a bounded application-owned adapter readiness/last-outcome projection; reading status must not perform TCP preflight or launch a process.
- Add deterministic runner/adapter tests using a controlled fake executable/helper process and component tests; no live DFHack in CI.
- Update the live validation runbook and require manual human validation.

## Out Of Scope

- No generic command endpoint/method, shell, dynamic script path, direct remote API, mutation command, backend script install/copy, model access, automatic retries, live CI, or UI beyond existing safe status.

## Boundaries And Invariants

- Executable and command mapping are closed/configured; all arguments use structured process APIs.
- Only reviewed read-only scripts are allowlisted.
- Host defaults to loopback; non-loopback requires a new explicit decision.
- Every network/process/read operation is bounded, cancellable, and output-limited.
- stdout remains untrusted until exit success, bounded JSON parse, schema validation, and mapping all succeed.
- Normal telemetry excludes command arguments containing identifiers, output, errors, paths, dwarf data, and process environment.

## Implementation Slices

### Slice 1: Closed runner and process classification

- Intended behavior: controlled helper process proves allowlist, structured args, capture, timeout, cancellation, output limit, and exit mapping.
- Likely files or modules touched: DwarfFortress runner/options/results, test helper, component tests.
- Test-first evidence: table-driven process-classification tests fail first.
- Completion evidence: no shell/generic command API exists and all classifications pass deterministically.

### Slice 2: TCP preflight and adapter mapping

- Intended behavior: unavailable preflight avoids process launch; valid JSON maps through B-020.
- Likely files or modules touched: preflight abstraction/implementation, adapter, tests.
- Test-first evidence: preflight false/no-launch, valid, invalid/oversize JSON, and cancellation tests fail first.
- Completion evidence: controlled tests prove ordering and stable mapping.

### Slice 3: Composition, telemetry, and live validation

- Intended behavior: configured live adapter is opt-in, observable, and manually verified against reviewed scripts.
- Likely files or modules touched: DI/options/status, telemetry tests, runbook.
- Test-first evidence: default-off, options validation, and redaction tests fail first.
- Completion evidence: automated checks pass and human records runbook results without raw private data.

## Acceptance Criteria

- [ ] Adapter lists eligible dwarves and fetches snapshots only by validated listed ID; it never reads DF UI cursor state.
- [ ] Exact closed allowlist and fixed executable invocation make arbitrary commands impossible.
- [ ] TCP preflight, timeout, cancellation, process-tree termination, and output bounds are tested.
- [ ] Start failure, unavailable, timeout, cancellation, crash, nonzero exit, oversize output, invalid JSON/schema, and mapping failure have stable categories.
- [ ] No process output, command arguments, private paths, or dwarf content enters normal telemetry/API errors.
- [ ] Adapter status exposes only allowlisted readiness/last-outcome metadata and performs no network/process work when read.
- [ ] Adapter status exposes only allowlisted readiness/last-outcome metadata and performs no network/process work when read.
- [ ] Automated tests use no live DFHack; human safety review and manual runbook validation are recorded.

## Test Strategy

Write classification tests first around a controlled helper executable invoked through the real process API. Do not mock `Process`; mock/substitute only TCP preflight or use a controlled local listener. Avoid sleeps by using synchronization signals and cancellation. Run canonical sample mapping tests and architecture/safety scans.

## Observability And Failure Behaviour

Use approved dwarf operation span(s) with adapter type, safe command enum (not raw text/args), outcome, duration, schema version, and stable error code. Preflight failure returns `DfHackUnavailable` without launch. Preserve internal causes securely but return bounded user guidance and correlation ID.

## Validation

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "DfHackProcess|DfHackAdapter|Safety"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-dfhack-samples.ps1
.\scripts\format.ps1
.\scripts\test.ps1
```

Manual verification must follow the updated DFHack runbook with a loaded fortress and must be explicitly reviewed by a human.

## Stop Conditions

- Stop if read-only behavior cannot be demonstrated or any generic/dynamic command surface is required.
- Stop if process cancellation/termination, output bounds, or path/secret redaction cannot be tested.
- Stop for human decision before changing ADR-0003 invocation strategy, allowing non-loopback RPC, or adding a dependency.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
