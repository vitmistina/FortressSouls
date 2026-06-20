# B-017: Add unified runtime diagnostics and failure UX

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-017`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Application status projection, API, and frontend diagnostics
Risk class: High
Recommended implementation model: Cross-module failure-design tier (GPT-5.5)
Recommended reasoning level: High
Recommended review model: Architecture/security review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

The browser shows safe adapter/provider readiness and last-operation diagnostics, while common dwarf-list, snapshot, session, prompt, provider, DFHack, and connectivity failures use one consistent accessible error experience with correlation IDs.

## Why This Slice Exists

B-023 is absorbed here. Each earlier boundary must already implement and test its own failures; this slice consolidates their safe public projection and browser presentation instead of postponing error handling to a broad cleanup pass.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-017/B-023 backlog rows
- completed B-015 browser chat, B-016 provider/status API, and B-021 DFHack adapter/status behavior
- `docs/specs/observability-v0.1.md`
- `docs/decisions/adr-0003-dfhack-adapter.md`
- `docs/decisions/adr-0005-llm-provider-strategy.md`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/frontend.instructions.md`
- `.agents/instructions/observability.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/skills/observability-first/SKILL.md`

Conditional context:

- existing public error DTOs, status projections, frontend query/state conventions, and provider/DFHack runbooks.

## Existing State To Inspect

Inventory all implemented stable error codes, HTTP mappings, adapter/provider status fields, frontend messages, log events, and correlation behavior before editing. Add characterization tests before consolidating existing behavior.

## In Scope

- Consolidate a finite application-owned public error/status projection without erasing adapter/provider causes internally.
- Expose or complete safe runtime status for configured adapter/provider type, model, readiness, stable last outcome/error category, bounded latency/time metadata, and correlation.
- Ensure status reads trigger no provider call, DFHack process, or file operation and remain process-local/non-persistent.
- Unify frontend loading, empty-list, selected-dwarf unavailable, backend unavailable, no fortress, invalid dwarf ID, invalid source/schema, invalid/expired session, prompt too large, provider missing/timeout/failure/invalid response, DFHack unavailable/timeout/failure, cancellation, and unexpected-failure presentation.
- Add accessible recovery actions only where behavior is deterministic: retry query, select another listed dwarf, create a new session, or open setup guidance.
- Add backend characterization/integration tests, frontend user-facing tests, and sentinel redaction tests.

## Out Of Scope

- No secret/model/endpoint editor, provider/adapter picker, network probe on status read, retry engine, notification framework, persistence, diagnostics download, or raw prompt/response/DFHack/provider content.

## Boundaries And Invariants

- Application owns stable categories; adapters keep implementation exceptions internal; API maps once; frontend maps codes to user language.
- Browser selection remains authoritative client state, but every selected ID is revalidated by the backend.
- Public status/errors never contain keys, headers, endpoint/path, command/arguments, raw output/body, prompts, responses, conversation, dwarf names, stack traces, or exception messages.
- Cancellation is not reported as an unexpected error.
- Last-operation state is bounded, thread-safe, process-local, and disappears on restart.

## Implementation Slices

### Slice 1: Characterize and consolidate backend projections

- Intended behavior: existing expected failures and runtime state map to one allowlisted public shape.
- Likely files or modules touched: Application status/error projection, API endpoints/middleware, integration/redaction tests.
- Test-first evidence: characterization tests capture current contracts, then table-driven tests fail for missing/inconsistent mappings.
- Completion evidence: status reads perform no external I/O and all public fields are allowlisted.

### Slice 2: Unified accessible frontend experience

- Intended behavior: runtime status and common failures are understandable and actionable without raw internals.
- Likely files or modules touched: diagnostics/error API clients, feature components, query integration, user-facing tests.
- Test-first evidence: loading/ready/degraded/missing/error and recovery-action tests fail first.
- Completion evidence: semantic status/error output and keyboard behavior pass.

### Slice 3: Telemetry and redaction review

- Intended behavior: severity, stable codes, and bounded fields are consistent across runtime boundaries.
- Likely files or modules touched: telemetry mapping and sentinel-content tests.
- Test-first evidence: sentinel secret/path/content tests expose any leaks before fixes.
- Completion evidence: telemetry explains outcome with no prohibited data.

## Acceptance Criteria

- [ ] Adapter/provider readiness and last safe outcomes are visible without triggering external work.
- [ ] Common dwarf, chat, provider, DFHack, and backend failures use stable public codes and accessible UI.
- [ ] Browser dwarf switching and invalid/stale selected IDs produce coherent recovery without mixed chat history.
- [ ] Correlation ID propagates through API, telemetry, and user-visible failure details.
- [ ] Tests cover every listed category, cancellation, unknown internal failure, and sensitive sentinel omission.
- [ ] No deferred broad retry/error framework or persistence is introduced.

## Test Strategy

Characterize existing mappings before consolidation. Use table-driven API integration tests and user-facing frontend tests. Replace adapters/providers only to trigger real boundary categories; do not mock central mapping. Sentinel values prove that secrets, paths, prompts, responses, conversation, and raw errors are omitted.

## Observability And Failure Behaviour

Use stable operation/error codes, bounded adapter/provider/model fields, duration, outcome, and correlation. Expected unavailable/validation states use appropriate Information/Warning severity; unexpected faults remain Error internally. Exception messages are never tags or user text.

## Validation

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "RuntimeStatus|ErrorMapping|Diagnostics|Redaction"
Set-Location .\src\frontend
npm test -- --run
npm run lint
npm run typecheck
npm run build
Set-Location ..\..
.\scripts\check.ps1
```

Manually exercise fake ready/empty/invalid-selection states plus controlled provider and DFHack unavailable states.

## Stop Conditions

- Stop if useful diagnostics would require exposing prohibited content or triggering an external probe.
- Stop if an earlier boundary lacks a stable application-owned failure category; fix it in that owning module rather than inventing UI parsing.
- Stop if a retry/fallback/persistence policy is required beyond accepted v0.1 behavior.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
