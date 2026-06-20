# B-008: Define dwarf contracts and JSON-file adapter

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-008`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Domain contracts and DwarfFortress file adapter
Risk class: High
Recommended implementation model: Trust-boundary implementation tier (GPT-5.5)
Recommended reasoning level: High
Recommended review model: Architecture/data-boundary review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

Versioned dwarf-list and snapshot contracts serialize deterministically, and a configured JSON-file adapter loads the retained DFHack list/snapshot samples into validated application-owned models with bounded, safe failures.

## Why This Slice Exists

B-020 is absorbed here so the contracts are proved against real captured schemas instead of being designed as disconnected types. The slice owns one trust boundary: untrusted JSON-file data into validated dwarf list and snapshot models.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-008/B-020 backlog rows
- `docs/decisions/adr-0002-modular-monolith.md`
- `docs/decisions/adr-0003-dfhack-adapter.md`
- `docs/research/dfhack-field-map.md`
- `dfhack/samples/dwarves-list.sample.json`
- `dfhack/samples/dwarf-snapshot.sample.json`
- `dfhack/samples/b019-dwarf-snapshots.bundle.json`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/dfhack.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/skills/modular-monolith-boundaries/SKILL.md`
- `.agents/skills/dfhack-adapter-safety/SKILL.md`

Conditional context:

- `docs/research/dfhack-live-state-probes.md` only when rejecting a field outside the accepted v0.1 mapping.

## Existing State To Inspect

The retained list, snapshot, and seven-snapshot bundle parse successfully, but no backend contracts or adapter exist. Inspect actual JSON shapes and provenance before naming application types. Preserve the canonical samples unchanged.

## In Scope

- Define compact application-owned contracts for `DwarfId`, dwarf summaries, list result/source metadata, snapshot identity/work/stress/skills/personality/needs/mannerisms/prompt candidates, and explicit schema versions.
- Define `IDwarfFortressAdapter` with `ListDwarvesAsync` and `GetDwarfSnapshotAsync(DwarfId, CancellationToken)`.
- Keep DFHack/file DTOs and JSON property details inside DwarfFortress and map them inward only after validation.
- Implement `JsonFileDwarfFortressAdapter` using configured list/snapshot paths that cannot come from an API caller.
- Bound file bytes, JSON depth, strings, collections, and identifiers; honor cancellation and reject unsupported schemas, malformed JSON, missing required identity, and list/snapshot ID mismatch.
- Add deterministic serialization, mapping, validation, and isolated real-filesystem component tests using copies of canonical samples.

## Out Of Scope

- No fake adapter, HTTP endpoints, frontend, process execution, file watching, write-back, schema repair, raw DFHack passthrough, or fields deferred by accepted research.
- No dependency on the unit highlighted in the Dwarf Fortress UI.

## Boundaries And Invariants

- The backend lists eligible dwarves; the browser selects one ID; snapshot extraction uses that validated ID.
- Domain/Application contracts contain no JSON, filesystem, DFHack, process, HTTP, provider, or telemetry implementation types.
- Files and file contents are untrusted, read-only, bounded, and cancellable.
- Optional data remains optional and never becomes an invented fact.
- Errors and telemetry omit file bodies, dwarf names, private paths, and raw validation payloads.

## Implementation Slices

### Slice 1: Versioned contracts and validation

- Intended behavior: valid list/snapshot values serialize consistently and invalid values fail with stable categories.
- Likely files or modules touched: Domain/Application contracts, validators, serialization/golden tests, architecture tests.
- Test-first evidence: write schema-version, required-field, limit, and dependency tests before the types/validators.
- Completion evidence: deterministic contract tests and inward-dependency checks pass.

### Slice 2: JSON-file adapter and canonical mapping

- Intended behavior: configured canonical list and snapshots map to the application contracts without mutation.
- Likely files or modules touched: DwarfFortress DTOs/mapper/adapter/options and component tests.
- Test-first evidence: valid, malformed, missing, oversized, unsupported-version, cancelled, and ID-mismatch tests fail first.
- Completion evidence: real serializer/filesystem tests pass against copied canonical samples.

## Acceptance Criteria

- [ ] Adapter port exposes only list and validated by-ID snapshot operations.
- [ ] Browser selection ownership and DF UI cursor independence are explicit in tests/contracts where relevant.
- [ ] Schema/source metadata and validation/size limits are explicit.
- [ ] Canonical list and snapshot samples map to application-owned contracts.
- [ ] Missing, inaccessible, malformed, oversized, unsupported, cancelled, and inconsistent files return stable safe errors.
- [ ] Adapter performs no process execution or file mutation and exposes no configured path publicly.
- [ ] Domain remains infrastructure-free.

## Test Strategy

Write contract and component tests first. Use real serializers, validators, and per-test temporary directories; do not mock value objects, JSON parsing, or filesystem reads. Review golden diffs manually and keep tests parallel-safe.

## Observability And Failure Behaviour

Callers may record adapter type `JsonFile`, operation, validated schema version, outcome, duration, and stable error code. Never record paths, JSON bodies, snapshots, or dwarf names. User messages distinguish unavailable, malformed, unsupported, and invalid data without machine details.

## Validation

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "DwarfContract|JsonFileDwarf|DwarfMapping|Architecture"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-dfhack-samples.ps1
.\scripts\format.ps1
.\scripts\test.ps1
```

## Stop Conditions

- Stop if canonical samples cannot map without changing an accepted schema/field decision.
- Stop if a proposed field lacks accepted research or a stable fixture.
- Stop if safe path policy, input bounds, cancellation, or stable error mapping cannot be demonstrated.
- Stop before exposing raw DTOs or browser-supplied file paths.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
