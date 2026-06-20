---
name: prompt-contracts
description: Decide whether and how a prompt change fits the deterministic v0.1 prompt contract.
---

# Prompt Contracts

## Decision

1. Reject inputs outside selected dwarf state, current conversation, static guide, and player message.
2. Normalize validated inputs before assembly; never let model output alter the template.
3. Add the smallest deterministic section that fixes the stated behavior.
4. Version externally visible template or schema changes.
5. Use a focused assertion for logic changes and a golden diff for assembled output changes.
6. Reject secrets, tools, persistence, hidden state, and default telemetry capture.

Check: identical inputs produce identical prompt text and tests show the intentional diff.
