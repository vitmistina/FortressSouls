# B-005: Add OpenTelemetry and structured logging baseline

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-005`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Observability
Risk class: Medium
Recommended implementation model: Cross-cutting implementation tier (GPT-5.4 high)
Recommended reasoning level: High
Recommended review model: Architecture-sensitive review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

The API emits structured, correlated logs and baseline trace/metric data; OTLP export is optional, console diagnostics remain usable, and telemetry contains no prohibited content.

## Why This Slice Exists

Later user-visible operations need stable instrumentation primitives before feature code spreads ad hoc names and fields. This slice owns registration, correlation, and redaction behavior. Each later feature owns the actual spans, metrics, and failure events for the boundary it introduces.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-005
- `docs/decisions/adr-0004-observability.md`
- `docs/specs/observability-v0.1.md`
- `docs/decisions/adr-0002-modular-monolith.md`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/observability.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/skills/observability-first/SKILL.md`

Conditional context:

- `docs/runbooks/local-dev.md` when documenting exporter configuration.
- B-004 implementation files and tests as they exist after that dependency completes.

## Existing State To Inspect

At planning time no backend exists. After B-004, inspect API startup, health response mapping, integration-test host, and the actual Observability project before adding packages or registration extensions.

## In Scope

- Add OpenTelemetry tracing and metrics registration in the Observability module and API composition root.
- Define shared `ActivitySource`, `Meter`, contract name constants, and stable field/tag names. Defer feature-specific instruments and operation spans to their owning backlog items.
- Add `X-Correlation-ID` middleware that accepts a bounded valid value or generates one, returns it in the response, and places it in log/trace context.
- Configure OTLP only when its endpoint is present and keep telemetry export non-fatal.
- Provide a console/local fallback consistent with ADR-0004.
- Add redaction/omission tests for prohibited fields and focused instrumentation tests.
- Extend health only with a bounded observability state, never exporter credentials or endpoints.

## Out Of Scope

- No production telemetry backend, collector, browser telemetry, retention, or full Aspire AppHost.
- No feature operation implementation, prompt/response capture, raw DFHack output, or unbounded metric labels.
- No logging middleware that records request/response bodies.

## Boundaries And Invariants

- Observability exposes inward-safe primitives; Domain has no telemetry SDK dependency.
- Never emit secrets, authorization headers, prompts, model responses, conversation text, dwarf names, raw DFHack output, arbitrary paths, or error messages as metric labels.
- Correlation IDs are untrusted input: validate character set and length before echoing or logging.
- Export failure cannot prevent API startup or normal request completion.
- Metric dimensions remain low-cardinality and contract-owned.

## Implementation Slices

### Slice 1: Telemetry primitives and registration

- Intended behavior: tracing/metrics sources register with optional OTLP export and safe fallback.
- Likely files or modules touched: Observability registration, API startup, configuration, project manifests.
- Test-first evidence: service-registration test initially fails because required sources/meters are absent.
- Completion evidence: registration tests and API startup without OTLP pass.

### Slice 2: Correlation and redaction

- Intended behavior: requests receive safe correlation context and prohibited data is omitted.
- Likely files or modules touched: middleware, log-scope helpers, integration tests.
- Test-first evidence: integration tests for generated, accepted, and rejected/oversized correlation IDs fail before middleware exists.
- Completion evidence: response header, trace/log context, and redaction checks pass.

## Acceptance Criteria

- [ ] API startup emits structured logs and succeeds with no OTLP endpoint.
- [ ] A health request creates trace data and returns a valid `X-Correlation-ID`.
- [ ] Shared sources, meters, contract names, and safe field definitions are registered once; feature instruments are not implemented prematurely.
- [ ] OTLP configuration follows ADR-0004 and export failure is non-fatal.
- [ ] Tests prove prohibited content is not emitted by project-owned telemetry helpers.
- [ ] Health exposes no endpoint, credential, secret, or environment value.

## Test Strategy

Start with correlation middleware integration tests and telemetry registration tests. Use an in-memory exporter/listener for project-owned spans and metrics instead of a live dashboard. Do not mock `ActivitySource`, `Meter`, or ASP.NET middleware; replace only the external exporter boundary.

## Observability And Failure Behaviour

Use contract names exactly. Record safe outcome, duration, operation, and relevant bounded identifiers only. Invalid incoming correlation IDs are replaced, not reflected. Export failures may produce a bounded warning without endpoint/path details and must not change the user response.

## Validation

Focused development checks:

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "Observability|Correlation|Redaction"
```

Final relevant checks:

```powershell
dotnet format .\src\backend\FortressSouls.sln --verify-no-changes
dotnet build .\src\backend\FortressSouls.sln
dotnet test .\src\backend\FortressSouls.sln --no-build
```

Manual verification: run the API without an OTLP endpoint, call health, then optionally follow `docs/runbooks/local-dev.md` to inspect one request in Aspire Dashboard and confirm no sensitive content.

## Stop Conditions

- Stop if implementation requires a collector, production backend, or full AppHost decision.
- Stop if a proposed tag/field is unbounded or contains user/model/game text.
- Stop if OpenTelemetry package choices conflict with the .NET target or accepted ADR.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
