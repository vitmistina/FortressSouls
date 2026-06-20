---
name: observability-first
description: Choose minimal telemetry that explains a user-visible operation without leaking sensitive data.
---

# Observability First

## Decision

1. Start from the question an operator must answer.
2. Use a trace for operation flow, a metric for trends, and a log for a discrete diagnostic event.
3. Reuse contract names and low-cardinality dimensions.
4. Record outcome, duration, and safe identifiers only when useful.
5. Redact or omit secrets, headers, prompts, responses, and unbounded values.
6. Add one focused check when redaction or instrumentation logic changes.

Check: telemetry explains success or failure without exposing content.
