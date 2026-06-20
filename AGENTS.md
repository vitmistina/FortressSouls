# FortressSouls Agent Guide

## Purpose

FortressSouls is the working repository for Fortress Souls v0.1: a read-only system that lets users inspect dwarves and chat with a dwarf persona derived from DFHack data.

## v0.1 Scope

- Read-only dwarf list and dwarf snapshot flows.
- Deterministic prompt assembly around validated DFHack data.
- Local-development-first scaffolding and documentation.

Not in scope for v0.1:

- Game-state mutation.
- Autonomous agent runtime features.
- Tool calling, streaming, persistence, or provider marketplace work unless a later backlog item explicitly adds them.

## Safety Rules

- Do not add code or scripts that mutate Dwarf Fortress state or saves.
- Preserve existing DFHack research, scripts, samples, README files, env files, and spike outputs unless a task explicitly says otherwise.
- Treat `docs/decisions/` as the record of accepted architecture decisions.
- When architecture, stack, or boundary decisions change, update the relevant ADR and supporting docs in the same task.

## Command Conventions

- Prefer repo-local scripts under `scripts/` over ad hoc commands when a script exists.
- Prefer `rg` for search and targeted inspection.
- Keep stable DFHack artifacts in `dfhack/`; keep stable documentation in `docs/`.
- Do not install dependencies or create application code during scaffold-only tasks.

## Engineering Contract

- Keep changes inside the current backlog item's scope and existing module boundaries.
- Validate inputs at trust boundaries; never expose secrets or sensitive prompt content.
- Add the smallest useful tests for changed behavior and run relevant checks before completion.
- Report changed files, validation results, and any known limitation.

## Where To Load Context

- Load `docs/specs/fortress-souls-v0.1.spec.md` for product scope and implementation constraints.
- Load `docs/backlog/v0.1-backlog.md` for task sequencing.
- Load `docs/decisions/` before changing architecture or stack assumptions.
- Load `docs/research/` and `docs/runbooks/` before touching DFHack-related work.
- Load the relevant file under `.agents/instructions/` for module work.
- Use `.agents/skills/` for reusable decisions, `.agents/agents/` for specialist roles, and `.agents/prompts/` for workflows.
