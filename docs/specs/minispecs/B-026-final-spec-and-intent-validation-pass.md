# B-026: Final spec and intent validation pass

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-026`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Whole-repository validation
Risk class: Critical
Recommended implementation model: Release/critical validation tier (GPT-5.5)
Recommended reasoning level: xhigh
Recommended review model: Independent cross-spec validation tier (GPT-5.5 xhigh)
Human checkpoint: Required

## Observable Outcome

A dated final validation artifact cross-checks implemented behavior and
supported runtime modes against the accepted product spec, accepted ADRs, and
current backlog-defined v0.1 slice, while also comparing all active mini-specs
as execution refinements subject to drift detection. Any mismatch, including
stale or conflicting mini-spec intent, is recorded as a finding with exact
evidence rather than hidden by rewriting accepted intent.

## Why This Slice Exists

B-025 release review evidence is necessary but not sufficient. B-026 runs
after that review as the final alignment check: it verifies that the accepted
scope from the product spec, accepted ADRs, and backlog still matches the
running product, and it surfaces drift in active execution plans explicitly
when they do not. Any misalignment found here is a finding that reopens the
B-025 release conclusion rather than coexisting harmlessly with an earlier GO.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-026
- all accepted ADRs
- `docs/specs/fortress-souls-v0.1.spec.md`
- `docs/architecture/0001-architecture-overview.md`
- `docs/backlog/v0.1-backlog.md`
- all active mini-specs linked from the backlog
- completed B-025 release review artifact and evidence
- implemented source, tests, scripts, manifests, public API contracts, and
  runbooks for the supported modes being validated
- `docs/runbooks/local-dev.md`
- `docs/runbooks/provider-configuration.md`
- `docs/runbooks/dfhack-b019-manual-validation.md`

Conditional context:

- provider and DFHack manual evidence only when it is dated,
  environment-scoped, and retained by prior review work
- CI history or git history only if available; do not infer results that are
  not present in the repository or local evidence set

## Existing State To Inspect

At planning time this item has no validation artifact yet. Start from the
accepted product spec, accepted ADRs, current backlog, completed B-025
evidence, and the actual repository tree. Build the comparison matrix from
those authoritative sources, compare the active mini-spec set as execution
guidance subject to drift reporting, and preserve any pre-existing worktree
changes instead of normalizing them away.

## In Scope

- Perform validation and create `docs/reviews/v0.1-final-spec-and-intent-validation.md`.
- Compare the accepted product spec, accepted ADRs, current backlog-defined
  slice, and every active mini-spec to the actual implementation, tests,
  scripts, runbooks, and observable runtime behavior, treating mini-spec drift
  as a finding rather than as new baseline intent.
- Verify that the running project matches the intended v0.1 slice in fake mode,
  and verify the documented optional provider and DFHack modes when their
  prerequisites and retained evidence are available.
- Check that browser-owned dwarf selection, by-ID snapshot retrieval,
  in-memory chat identity binding, diagnostics, and read-only boundaries match
  the accepted scope.
- Record mismatches, missing evidence, stale mini-spec intent, or unsupported
  behavior as explicit findings with severity and follow-up needs.

## Out Of Scope

- No implementation fixes, hidden behavior changes, silent spec rewrites,
  backlog status laundering, dependency upgrades, release tagging, or new
  claims about manual/live modes without recorded evidence.
- No folding unrelated cleanup or late feature work into this validation item.

## Boundaries And Invariants

- Accepted product intent remains in the product spec, accepted ADRs, and the
  backlog; active mini-specs refine execution and are themselves subject to
  drift detection. Mismatches, including stale or conflicting mini-spec intent,
  are recorded as findings rather than solved by rewriting intent during this
  pass.
- B-025 provides prerequisite evidence and an initial release conclusion, but
  any misalignment found here reopens that conclusion as a finding for human
  disposition.
- No mutation, arbitrary command execution, model tool access, persistence,
  streaming, secret/content telemetry, or hidden scope expansion may be treated
  as acceptable drift for v0.1.
- A missing or unrun check is recorded as missing evidence, never as a pass.
- This item validates behavior; it does not hide code, contract, or document
  changes inside the review.
- Optional modes must be labeled accurately as verified, not run, or blocked by
  missing prerequisites.

## Implementation Slices

### Slice 1: Spec and mini-spec comparison matrix

- Intended behavior: the accepted product spec and every active mini-spec map
  to current implementation evidence or a finding.
- Likely files or modules touched: final validation artifact only.
- Test-first evidence: review/documentation exception; establish the comparison
  matrix before running workflows.
- Completion evidence: every active mini-spec and each core v0.1 behavior area
  is classified as aligned, mismatched, or missing evidence with citations.

### Slice 2: Supported runtime verification

- Intended behavior: the project, when run in its supported modes, behaves the
  way the accepted v0.1 slice says it should.
- Likely files or modules touched: final validation artifact only.
- Test-first evidence: execute the documented workflows instead of trusting
  prior prose summaries.
- Completion evidence: fake mode is rerun and recorded; optional provider and
  DFHack modes are either rerun with evidence or clearly labeled as not run.

### Slice 3: Findings and final disposition

- Intended behavior: mismatches are severity-ordered, no intent is rewritten,
  and the final disposition is explicit.
- Likely files or modules touched: `docs/reviews/v0.1-final-spec-and-intent-validation.md`.
- Test-first evidence: cross-check every apparent mismatch against ADRs, the
  product spec, the owning mini-spec, and B-025 evidence before classifying it.
- Completion evidence: a human can see whether the implemented project matches
  the intended v0.1 slice and what follow-up work is required.

## Acceptance Criteria

- [ ] Final validation artifact compares `docs/specs/fortress-souls-v0.1.spec.md`
  and every active backlog-linked mini-spec against current implementation
  evidence, treating stale or conflicting mini-spec intent as findings
  rather than as baseline product intent.
- [ ] Fake mode is verified again from the supported repository workflow, and
      optional provider and DFHack modes are recorded as verified, not run, or
      blocked with concrete reasons.
- [ ] Browser-owned dwarf selection, by-ID snapshot retrieval, selected-dwarf
      chat identity binding, diagnostics, and v0.1 non-goals are explicitly
      cross-checked against observable behavior.
- [ ] Any drift, stale intent, unsupported behavior, or missing evidence is
      recorded as a finding rather than corrected or obscured inside this item.
- [ ] Exact commands, dates, environments, and results are recorded, and no
      unrun check is represented as passed.
- [ ] Final disposition clearly states whether the implemented repository is
      aligned with the intended v0.1 slice and what human follow-up is needed.

## Test Strategy

This is a review/validation item, so red-green-refactor is not applicable.
Correctness evidence comes from accepted-document comparison, canonical
repository scripts, focused workflow execution, targeted structural searches,
and runtime observation. Do not change implementation to make the matrix look
cleaner; record the discrepancy.

## Observability And Failure Behaviour

Use existing safe diagnostics, logs, traces, and metrics to confirm behavior
and boundaries. Record event names, status surfaces, and failure categories
only. Do not store prompts, responses, secrets, raw DFHack output, private
paths, or conversation content in the final validation artifact.

## Validation

Run the canonical repository scripts that exist at validation time, expected to
include:

```powershell
.\scripts\format.ps1
.\scripts\test.ps1
.\scripts\check.ps1
```

Then perform repository-specific workflow and comparison validation:

- Run the focused fake-mode browser smoke flow from `src/frontend`:

```powershell
npm run test:e2e:install
npm run test:e2e
```

- Start the documented fake-mode application workflow from
  `docs/runbooks/local-dev.md` and verify health/status, browser dwarf listing,
  browser-owned selection, snapshot-by-ID behavior, selected-dwarf identity in
  chat, diagnostics surfaces, and the absence of streaming/persistence/tool
  behavior.
- When prerequisites and retained B-025 evidence exist, rerun the documented
  optional real-provider and approved DFHack workflows and record exact
  outcomes. If a mode cannot be rerun safely, mark it `Not Run` with the reason.
- Build a written comparison matrix from
  `docs/specs/fortress-souls-v0.1.spec.md`, the active mini-spec set, current
  implementation files, tests, scripts, runbooks, and B-025 evidence.
- Use repository search to confirm there is no undocumented mutation surface,
  generic command execution surface, model tool path, persistent chat storage,
  or streaming behavior, and record the exact search terms used.
- Record every exact command result and any validation limits in the artifact.

## Stop Conditions

- Stop if B-025 release review evidence does not exist or is too incomplete to
  support this final comparison pass.
- Stop and record findings if accepted product intent materially conflicts with
  implemented behavior; do not rewrite intent inside this item.
- Stop with a not-aligned conclusion for any weakened read-only/safety boundary
  or any required supported-mode verification that cannot be established.
- Stop rather than implementing or smuggling fixes into the validation pass.

## Completion Report

Report: 1. outcome and final alignment disposition; 2. changed validation files;
3. exact validation commands and workflow results; 4. findings, unverified
assumptions, remaining manual checks, and required human follow-up.