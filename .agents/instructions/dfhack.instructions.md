---
description: "Use when working with DFHack scripts, adapters, research, or sample data. Covers read-only constraints, process safety, input validation, artifact preservation, and manual validation."
applyTo: "dfhack/**"
---

# DFHack Instructions

Scope: DFHack research, scripts, adapters, and sample data.

- Read `docs/research/` and `docs/runbooks/` before editing.
- v0.1 is read-only: reject commands or APIs that can mutate game state or saves.
- Keep process invocation allowlisted, argument-safe, time-bounded, and cancellable.
- Treat DFHack output as untrusted; validate and map it before domain use.
- Preserve raw research artifacts and stable samples unless the task says otherwise.
- Validate scripts manually per the runbook and fixtures automatically where possible.
