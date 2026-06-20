# Project Memory

Last updated: 2026-06-19

## Initial Decisions

- B-001 is implemented as a reconciliation task, not as a blank-repo bootstrap.
- Existing DFHack research, scripts, sample JSON, README files, env files, and spike outputs are preserved assets.
- The existing root spec `v0.1.spec.md` is preserved; a scaffold copy now exists at `docs/specs/fortress-souls-v0.1.spec.md`.
- The repository already contains accepted v0.1 documentation beyond B-001, including `adr-0003`, `adr-0004`, and `adr-0005`.
- `dfhack/samples/` remains the current home for DFHack adapter artifacts; root `samples/` is reserved for future application-facing examples.
- No application code or dependency installation is allowed in B-001.

## Open Follow-Ups

- Add the agent primitive files in B-002.
- Add the missing scaffold ADRs and architecture docs in B-003.
- Decide later whether the root spec copy should be retired after the docs tree becomes canonical.
