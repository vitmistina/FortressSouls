# B-006: Add local dev orchestration

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-006`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Developer tooling
Risk class: Low
Recommended implementation model: Mechanical implementation tier (GPT-5.4 mini)
Recommended reasoning level: Medium
Recommended review model: Bounded review tier (GPT-5.4 high)
Human checkpoint: No

## Observable Outcome

A developer can start the current local application and run the current repository checks through documented PowerShell and shell entry points, without requiring Docker, Aspire, DFHack, or provider credentials.

## Why This Slice Exists

Canonical commands keep local and CI behavior aligned. The smallest durable path is thin repository scripts around the actual backend/frontend commands, with the standalone Aspire Dashboard remaining optional.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-006
- `docs/decisions/adr-0004-observability.md`
- `docs/runbooks/local-dev.md`
- actual backend solution and tests after B-005
- actual frontend package scripts and lockfile after B-007

Conditional context:

- CI configuration only if it already exists and invokes repository commands.

## Existing State To Inspect

At planning time only DFHack maintainer PowerShell scripts exist; canonical `dev`, `format`, `test`, and `check` wrappers do not. Recheck the tree, package manager lockfile, solution path, and current README/runbook before editing.

## In Scope

- Add `scripts/dev.ps1`, `dev.sh`, `format.ps1`, `format.sh`, `test.ps1`, `test.sh`, `check.ps1`, and `check.sh` as thin fail-fast wrappers.
- Start the current backend and frontend shells with DFHack/provider integrations disabled or not configured; B-009 and B-014 later extend the same commands into full fake mode.
- Keep OTLP/Aspire Dashboard optional and run both modules' established checks.
- Update `docs/runbooks/local-dev.md` and the current repository entry README if one exists.
- Keep direct `dotnet run` and module-specific commands documented for troubleshooting.

## Out Of Scope

- No AppHost, ServiceDefaults, Compose, cloud resource, production deployment, package upgrade, or automatic dependency installation.
- No background process manager beyond what a small local start wrapper needs.
- No DFHack script installation or live provider startup.

## Boundaries And Invariants

- Tooling does not create service boundaries or make telemetry infrastructure mandatory.
- Scripts invoke fixed repository commands and do not evaluate untrusted text.
- Scripts fail on real build/test/lint failures and do not skip missing expected modules silently.
- Secrets remain environment-provided and are never printed.

## Implementation Slices

### Slice 1: Canonical check wrappers

- Intended behavior: format, test, and check wrappers produce consistent exit codes on Windows and POSIX shells.
- Likely files or modules touched: `scripts/*`, existing build/package manifests only if a script name is missing.
- Test-first evidence: no unit test is useful for thin wrappers; first capture current direct commands and deliberately verify that a failing child command propagates non-zero in a controlled invocation.
- Completion evidence: wrappers run the existing modules and return their exit codes.

### Slice 2: Local start workflow and docs

- Intended behavior: fake-mode startup is one documented command and dashboard setup stays optional.
- Likely files or modules touched: `scripts/dev.*`, local-dev runbook, repository README if present.
- Test-first evidence: documentation/tooling exception; validate from a clean shell rather than adding brittle script-content tests.
- Completion evidence: startup reaches health and the docs match the commands exactly.

## Acceptance Criteria

- [ ] PowerShell and shell wrappers exist for `dev`, `format`, `test`, and `check`.
- [ ] Current backend and frontend shells start together with one documented command and no external service.
- [ ] Test/check wrappers fail when a child check fails.
- [ ] Frontend lint/type/test/build and backend format/build/test are included in the canonical wrappers.
- [ ] Local dashboard use is documented but optional.
- [ ] No AppHost or Compose complexity is added without a new decision.

## Test Strategy

This is thin tooling and documentation, so behavior-first shell verification replaces unit tests. Exercise success and controlled failure exit-code propagation. Do not mock the underlying build tools; the wrappers are valuable only if they run the real commands.

## Observability And Failure Behaviour

Scripts print concise command-stage failures but never dump environment variables. Missing required SDKs/runtimes produce actionable messages. Dashboard unavailability does not fail application startup.

## Validation

Focused development checks:

```powershell
.\scripts\format.ps1
.\scripts\test.ps1
```

Final relevant checks:

```powershell
.\scripts\check.ps1
```

On a POSIX shell, also run `./scripts/check.sh`. Manually start `./scripts/dev.ps1`, call health, and stop the process cleanly.

## Stop Conditions

- Stop if one-command startup materially requires AppHost, Compose, or another orchestration dependency.
- Stop if backend and frontend package-manager conventions conflict.
- Stop rather than adding silent skip logic for a module that should exist by the current backlog state.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
