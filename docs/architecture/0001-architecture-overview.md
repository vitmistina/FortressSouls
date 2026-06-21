# Architecture Overview

## Intent

Fortress Souls v0.1 is a local, read-only companion for Dwarf Fortress. The
browser selects a dwarf from a backend-provided list, the backend fetches a
validated snapshot for that dwarf ID, deterministic application code assembles
prompt context, and one configured provider generates the dwarf's prose reply.

## Current Repository Shape

- `docs/` holds stable specs, ADRs, runbooks, and research.
- `dfhack/` holds the allowlisted read-only Lua scripts plus retained manual
  validation samples.
- `samples/` holds fake app-facing dwarf list and snapshot examples for offline
  development and tests.
- `scripts/` holds canonical local dev, format, test, and check entry points,
  plus maintainer utilities.
- `src/backend/` holds the modular monolith backend, adapters, observability,
  prompting, and automated tests.
- `src/frontend/` holds the local React/Vite UI and browser tests.
- `.agents/` holds repository guidance, memories, prompts, skills, and agent
  definitions.

## v0.1 Architectural Direction

- One backend deployment unit with explicit internal module boundaries.
- Read-only adapter sequence: `Fake`, `JsonFile`, and optional `DfHackProcess`.
- The browser owns dwarf identity selection; the Dwarf Fortress UI cursor is
  not an application input.
- Deterministic contracts govern dwarf list/snapshot data, prompt assembly,
  session state, and runtime status projections.
- Observability is built in from the first backend slice with correlation IDs,
  structured logs, traces, and metrics.
- Fake mode is the default supported development path; real provider and live
  DFHack modes remain optional and separately documented.

## Constraints

- Keep game mutation impossible by construction.
- Do not add generic DFHack execution or model tool surfaces.
- Keep prompt/response content and secrets out of default telemetry.
- If the architecture direction changes, update this document and the relevant
  ADRs in the same change set.
