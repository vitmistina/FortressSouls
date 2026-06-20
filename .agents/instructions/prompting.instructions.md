---
description: "Use when writing LLM prompts, prompt contracts, assembly logic, or prompt tests. Covers deterministic assembly, redaction, schema versioning, and golden tests."
applyTo: "src/**/prompt**"
---

# Prompting Instructions

Scope: prompt contracts, assembly, previews, and prompt tests.

- Keep assembly deterministic and separate from model invocation.
- Use only the selected dwarf state, current in-memory conversation, static guide, and player message.
- Exclude secrets, hidden fortress state, tools, persistence, and raw DFHack data by default.
- Keep template and schema versions explicit.
- Treat prompt changes as contract changes with focused or golden tests.
- Do not log full prompts; developer preview must be explicit.
