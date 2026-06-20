# B-009: Implement fake dwarf adapter and dwarf API

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-009`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: DwarfFortress fake, Application queries, and API
Risk class: High
Recommended implementation model: Backend vertical-slice tier (GPT-5.4 high)
Recommended reasoning level: High
Recommended review model: Architecture-sensitive review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

In fake mode, `GET /api/dwarves` returns a stable synthetic list and `GET /api/dwarves/{dwarfId}/snapshot` returns the matching validated snapshot with source/schema metadata and safe errors.

## Why This Slice Exists

B-010 is absorbed here so the permanent fake is validated through an observable HTTP contract rather than existing as unused test scaffolding. This is the backend vertical slice consumed by the first real frontend feature.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-009/B-010 backlog rows
- completed B-005 observability baseline and B-008 contracts/port
- `docs/specs/fortress-souls-v0.1.spec.md`, API contract sections 8.2 and 8.3
- `docs/specs/observability-v0.1.md`
- `docs/decisions/adr-0002-modular-monolith.md`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/instructions/observability.instructions.md`
- `.agents/skills/observability-first/SKILL.md`

Conditional context:

- `docs/research/dfhack-field-map.md` when choosing realistic but synthetic ranges.

## Existing State To Inspect

After B-008, inspect the actual adapter port, validation/errors, API health conventions, correlation middleware, serializer options, and test host. Retained `dfhack/samples/` are live-validation artifacts, not fake data.

## In Scope

- Implement a deterministic no-I/O `FakeDwarfFortressAdapter` with at least three clearly synthetic, distinct dwarves.
- Keep synthetic fixtures app-facing and provenance-clear; do not copy live identities from retained DFHack samples.
- Add Application list/snapshot queries and thin API endpoints matching the documented public contract.
- Validate route IDs and map no dwarves, invalid ID, malformed fake configuration, and cancellation to stable public errors.
- Return schema/source metadata and propagate request cancellation.
- Instrument list/snapshot operations with contract span/metric names, safe adapter/schema/outcome fields, and correlation.
- Add port tests and full API integration tests through the real fake, application use cases, serializer, and ASP.NET pipeline.

## Out Of Scope

- No JSON/process adapter changes, frontend, chat, real provider, random fixture generation, raw domain serialization, or game/DF UI selection state.

## Boundaries And Invariants

- The frontend will own selection from the returned list; the API accepts only a validated listed dwarf ID for snapshot lookup.
- Fake data is deterministic, synthetic, bounded, and independent of clock, random, network, filesystem, process, and environment.
- Endpoints remain transport mapping only; Application owns use-case/error behavior.
- Public errors expose no stack trace, internal type, path, or raw payload.
- Telemetry excludes dwarf names and snapshot contents.

## Implementation Slices

### Slice 1: Deterministic fake and fixtures

- Intended behavior: list, by-ID snapshot, empty/not-found, and cancellation behavior satisfy the B-008 port.
- Likely files or modules touched: DwarfFortress fake/fixtures/registration and port tests.
- Test-first evidence: stable ordering, valid lookup, missing ID, empty state, cancellation, and no-I/O tests fail first.
- Completion evidence: repeated calls return identical validated results.

### Slice 2: List/snapshot API vertical slice

- Intended behavior: documented HTTP endpoints expose fake data and stable failures.
- Likely files or modules touched: Application queries, API DTOs/endpoints, integration and telemetry tests.
- Test-first evidence: success/error HTTP contract tests fail before implementation.
- Completion evidence: integration tests prove JSON shape, correlation, cancellation, and safe telemetry.

## Acceptance Criteria

- [ ] Fake returns a stable list of at least three distinct synthetic dwarves.
- [ ] Valid listed ID returns its matching validated snapshot; invalid ID returns the stable public error.
- [ ] `GET /api/dwarves` and `GET /api/dwarves/{dwarfId}/snapshot` match the documented JSON contract.
- [ ] Responses include adapter/source/schema metadata.
- [ ] Fake mode performs no external I/O and requires no secrets or Dwarf Fortress.
- [ ] Integration tests cover list, empty, snapshot, invalid ID, cancellation, and telemetry/redaction.

## Test Strategy

Write fake port tests and HTTP integration tests first. Use the real fake, mapper, use cases, serializer, middleware, and test server. Substitute the adapter only for targeted failure injection; do not mock the endpoint pipeline or fake itself.

## Observability And Failure Behaviour

Use `fortresssouls.dwarves.list` and `fortresssouls.dwarves.snapshot` with adapter type, schema version, outcome, duration, and correlation ID. Never tag dwarf name, snapshot, browser input text, or exception message. Public failures use stable codes and safe messages.

## Validation

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "FakeDwarf|DwarfApi|DwarfQuery|Redaction"
.\scripts\format.ps1
.\scripts\test.ps1
```

Manually call both endpoints in fake mode and compare their shape with the API contract.

## Stop Conditions

- Stop if the API would expose adapter DTOs or configured implementation details.
- Stop if fake controls require a production mutation/debug endpoint.
- Stop if synthetic fixtures rely on unverified/deferred fields.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
