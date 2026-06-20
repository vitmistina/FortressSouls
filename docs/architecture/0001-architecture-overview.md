# Architecture Overview

## Intent

This repository is being reconciled into the Fortress Souls v0.1 scaffold around existing DFHack research and validated read-only artifacts. B-001 does not add application code.

## Current Shape

- `docs/` holds stable specs, ADRs, runbooks, and research.
- `dfhack/` holds production-oriented read-only Lua scripts and sample DFHack outputs.
- `scripts/` holds maintainer utilities.
- `agent/` is reserved for future task instructions, agent definitions, skills, prompts, and project memory.
- `src/` is reserved for future application code and remains intentionally empty in B-001.
- `samples/` is reserved for future app-facing prompt and snapshot examples and remains intentionally empty in B-001.

## v0.1 Architectural Direction

- Modular monolith monorepo.
- Read-only DFHack integration.
- Deterministic data contracts feeding prompt assembly.
- Observability from the first backend slice.

## Reconciliation Constraints

- Preserve existing research, samples, and spike outputs.
- Do not rename or move existing files during B-001 unless a later task explicitly approves it.
- If the architecture direction changes, update this document and the relevant ADRs in the same change set.
