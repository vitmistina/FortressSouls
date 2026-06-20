---
name: dfhack-adapter-safety
description: Decide whether a DFHack integration is verifiably read-only and safe for the v0.1 adapter.
---

# DFHack Adapter Safety

## Decision

1. Identify the exact command, arguments, data read, and failure modes.
2. Reject mutation, console passthrough, dynamic command text, or save writes.
3. Prefer an existing researched script; otherwise add the narrowest read-only probe.
4. Allowlist invocation and enforce timeout, cancellation, exit-code, and output limits.
5. Validate output before mapping; tolerate documented optional fields only.
6. Verify against stable samples, then follow the manual runbook when live validation is needed.

Stop when read-only behavior or invocation cannot be demonstrated.
