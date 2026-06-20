---
description: "Use when adding logging, tracing, metrics, telemetry, health checks, or diagnostics. Covers correlation context, low-cardinality tags, redaction, and boundary instrumentation."
applyTo: "src/**/observability/**"
---

# Observability Instructions

Scope: logs, traces, metrics, health, and diagnostics.

- Instrument user-visible and external-boundary operations with stable names.
- Propagate correlation and cancellation context.
- Record useful low-cardinality tags and actionable failure details.
- Never emit secrets, authorization headers, full prompts, or full model responses by default.
- Test redaction and important instrumentation behavior.
- Follow `docs/specs/observability-v0.1.md` and `docs/decisions/adr-0004-observability.md`.
