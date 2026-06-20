# B-024: Verify documentation and runbooks

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-024`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Documentation
Risk class: Medium
Recommended implementation model: Bounded documentation tier (GPT-5.4 high)
Recommended reasoning level: High
Recommended review model: Architecture-sensitive documentation review tier (GPT-5.5 high)
Human checkpoint: No

## Observable Outcome

A new developer can follow the repository documentation to run fake mode, optional real-provider mode, and approved DFHack mode; the docs accurately state backend listing, browser selection, safety guarantees, diagnostics, and limitations.

## Why This Slice Exists

Release documentation must verify tested reality rather than become a deferred catch-all for behavior or setup decisions that should have been documented by their owning backlog items.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-024
- all accepted ADRs
- `docs/specs/fortress-souls-v0.1.spec.md`
- completed B-017 unified diagnostics behavior and B-022 fake browser proof
- implemented source, manifests, scripts, tests, and public API contracts
- `docs/runbooks/local-dev.md`
- `docs/runbooks/provider-configuration.md`
- `docs/runbooks/dfhack-b019-manual-validation.md`
- `.agents/memory/project.memory.md`
- `.agents/prompts/update-memory.prompt.md`

Conditional context:

- existing root README, `docs/runbooks/dfhack-setup.md`, and `docs/runbooks/troubleshooting.md` if created before this item.
- release/environment configuration examples actually committed by implementation items.

## Existing State To Inspect

At planning time there is no root README, DFHack setup runbook, or troubleshooting runbook; local-dev/provider/B-019 runbooks are draft and include pre-implementation commands. Recheck existence and verify every command/config key against the final tree before editing.

## In Scope

- Create/update root README with purpose, read-only guarantee, v0.1 boundaries, prerequisites, fake-mode quickstart, checks, and links.
- Verify and close gaps in local development, provider configuration, DFHack setup/manual validation, and troubleshooting runbooks against implemented commands and safe recovery.
- Update product spec, architecture docs, accepted decision references, samples/schema links, and project memory only where final implementation evidence shows drift.
- Document loading/empty/degraded/error behavior, configuration keys without values, diagnostics/correlation use, manual checks, and known limitations.
- Validate every internal link, command path, file name/case, and configuration name.

## Out Of Scope

- No application/code/config behavior changes, feature work, marketing site, benchmark essay, secret example, machine-specific private path as a required default, or rewriting decision history without a superseding ADR.

## Boundaries And Invariants

- Accepted ADRs and implemented contracts override older draft/scaffold prose.
- Documentation never claims live provider/DFHack verification that was not recorded.
- Examples contain placeholders only and do not print secret-check commands.
- Read-only/no-tools/no-persistence/no-streaming/no-mutation boundaries are explicit.
- Commands must exist and be runnable in the current repository.

## Implementation Slices

### Slice 1: Authoritative behavior and entry documentation

- Intended behavior: README/spec/architecture agree on backend list/browser selection and the real quickstart.
- Likely files or modules touched: README, product spec, architecture overview, project memory.
- Test-first evidence: documentation exception; build an evidence matrix of claims to implemented files/tests before editing.
- Completion evidence: no contradictory DF-cursor/browser-selection or scope statements remain in authoritative docs.

### Slice 2: Operational runbooks

- Intended behavior: fake, provider, DFHack, telemetry, and troubleshooting steps match reality.
- Likely files or modules touched: runbooks and safe configuration examples.
- Test-first evidence: run commands from a clean shell as executable documentation.
- Completion evidence: fake quickstart/checks pass and optional modes clearly separate unverified/manual steps.

### Slice 3: Link/consistency audit

- Intended behavior: links/case/commands/config names resolve and docs contain no secrets/private required paths.
- Likely files or modules touched: documentation only.
- Test-first evidence: run structural/link checks before and after edits.
- Completion evidence: all checks pass and final diff contains no application code.

## Acceptance Criteria

- [ ] New developer can run fake mode and repository checks from README commands.
- [ ] Provider and DFHack modes document prerequisites, safe configuration, failures, and manual verification accurately.
- [ ] Troubleshooting uses stable errors/correlation without exposing secrets/content/paths.
- [ ] All internal links, command paths, file case, and configuration names validate.
- [ ] Docs clearly state v0.1 limits and distinguish automated evidence from manual/unverified behavior.

## Test Strategy

Test-first is not useful for prose. Use command execution, link checking, structural searches, and claim-to-evidence review. Do not alter implementation merely to make a stale documentation command pass; correct the docs or surface a real implementation defect separately.

## Observability And Failure Behaviour

Document how to inspect safe logs/traces/metrics and correlation IDs, plus prohibited telemetry. Do not instruct users to print environment secrets, prompts, responses, raw DFHack output, or private paths into reports.

## Validation

```powershell
.\scripts\check.ps1
rg -n "browser.*select|list dwarves|POST /api/dfhack/execute|streaming|persistent chat" AGENTS.md docs README.md
git diff --check
```

Run the README fake-mode quickstart from a clean shell where practical and execute the repository's link checker if one exists; otherwise use a local structural link script/check without network access.

## Stop Conditions

- Stop if documented setup cannot be verified and no accurate limitation/manual label is possible.
- Stop if completing docs would require hidden application implementation.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
