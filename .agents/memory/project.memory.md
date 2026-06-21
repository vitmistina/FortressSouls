# Project Memory

Last updated: 2026-06-20

## Initial Decisions

- B-001 is implemented as a reconciliation task, not as a blank-repo bootstrap.
- Existing DFHack research, scripts, sample JSON, README files, env files, and spike outputs are preserved assets.
- The canonical v0.1 spec currently lives at `docs/specs/fortress-souls-v0.1.spec.md`; there is no separate root `v0.1.spec.md` in this workspace.
- The repository already contains accepted v0.1 documentation beyond B-001, including `adr-0003`, `adr-0004`, and `adr-0005`.
- `dfhack/samples/` remains the current home for DFHack adapter artifacts; root `samples/` is reserved for future application-facing examples.
- No application code or dependency installation is allowed in B-001.

## Current v0.1 Decisions

- The backend lists eligible dwarves and fetches snapshots by validated dwarf
  ID. The browser owns selection; the unit highlighted in the Dwarf Fortress UI
  is not an input.
- Fresh workspaces require a one-time `npm install` in `src/frontend` before
  the canonical dev, format, test, and check commands can complete
  successfully.
- B-018 and B-019 are complete and their retained scripts/samples inform the
  application contracts.
- Remaining work follows dependency-driven waves rather than numeric phases.
- B-020 is merged into B-008, B-010 into B-009, B-013 into B-014, and B-023
  into B-017. Stable IDs remain in the backlog as merged historical rows.
- B-005 and B-007 complete in parallel after B-004; B-006 then establishes the
  combined backend/frontend local development loop before feature work.

## Open Follow-Ups

- Implement the active mini-specs in the dependency order recorded in
  `docs/backlog/v0.1-backlog.md`.
