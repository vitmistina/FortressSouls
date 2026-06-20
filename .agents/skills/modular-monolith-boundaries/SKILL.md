---
name: modular-monolith-boundaries
description: Decide where backend behavior belongs without eroding Fortress Souls module boundaries.
---

# Modular Monolith Boundaries

## Decision

1. Put pure game concepts in Domain.
2. Put DFHack and file access behind DwarfFortress adapters.
3. Put deterministic prompt construction in Prompting and provider calls in LLM.
4. Put use-case coordination in Chat/Application and transport wiring in API.
5. If two modules need the behavior, depend on a small stable contract; do not add a shared dumping ground.
6. If dependency direction or ownership changes, stop and record an ADR.

Check: domain stays infrastructure-free, dependencies point inward, and tests can replace external boundaries.
