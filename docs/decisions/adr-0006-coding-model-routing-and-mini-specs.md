# ADR-0006: Coding Model Routing and Mini-Specs

Status: Accepted
Date: 2026-06-20

## Context

Fortress Souls uses coding models with different cost, latency, availability, and reasoning capacity. Sending every task to the strongest route wastes capacity, while sending an ambiguous or safety-sensitive task to a smaller model increases rework and can erode architecture or the read-only boundary.

Backlog rows provide priority and acceptance intent, but non-trivial implementation work needs a smaller execution contract. A coding model should not have to redesign selection semantics, module ownership, failure policy, or trust-boundary behavior while implementing a backlog item.

## Decision

Use larger models for decomposition, consequential decision synthesis, architecture-sensitive design, critical safety review, and release review. Use smaller models for precise, bounded implementation when the mini-spec has removed material ambiguity.

Every non-trivial implementation backlog item receives one mini-spec keyed by its backlog ID. The mini-spec refines its parent item with observable behavior, exact context, scope, invariants, ordered slices, test evidence, validation, routing, and stop conditions. It does not replace or override the backlog, accepted ADRs, product contracts, or repository instructions.

When two backlog items are merged, keep both stable IDs in the backlog, mark
the absorbed row `MERGED INTO B-XXX`, and point it to the surviving mini-spec.
Remove the duplicate execution plan so models cannot implement both scopes.

Routing is based on ambiguity, architectural reach, trust-boundary and safety risk, ease of automated verification, expected rework, and current model cost, latency, and availability. File count is not a routing criterion by itself.

## Mini-Spec Lifecycle

1. **Plan:** A decomposition-capable model compares the backlog with current code, tests, contracts, and accepted decisions, then writes or updates the mini-spec.
2. **Human decision:** A human resolves consequential product, architecture, dependency, provider-security, process-execution, or DFHack safety questions identified by the mini-spec.
3. **Implement:** The routed model implements one coherent mini-spec or its explicitly ordered slices.
4. **Validate:** The implementation model runs the focused and final checks named by the mini-spec and records exact results.
5. **Review:** A risk-appropriate stronger model reviews contracts, boundaries, safety, tests, and diff scope. Critical items also require a human checkpoint.
6. **Update evidence:** Only demonstrated acceptance evidence changes backlog status. Keep the mini-spec, docs, contracts, and runbooks synchronized with intentional behavior changes.

## Routing Guide

| Work characteristics | Default implementation route | Default review route |
| --- | --- | --- |
| Mechanical, local, precise, low risk | Mechanical tier (GPT-5.4 mini), low/medium reasoning | GPT-5.4 |
| Bounded implementation, moderate ambiguity | General implementation tier (GPT-5.4), medium/high reasoning | GPT-5.4 high or GPT-5.5 high |
| Cross-module contract, prompting, observability, complex integration | Cross-cutting tier (GPT-5.4 high or GPT-5.5), high reasoning | GPT-5.5 high |
| Architecture-sensitive, provider-security, process-execution, DFHack safety | Safety/architecture tier (GPT-5.5), high reasoning; xhigh only when justified | GPT-5.5 high/xhigh plus human checkpoint |
| Backlog decomposition, consequential ADR synthesis, critical safety or release review | Planning/review tier (GPT-5.5), xhigh reasoning | Independent GPT-5.5 xhigh and human decision |

Current model names are operational examples, not permanent architecture. Equivalent available models may be substituted when they meet the same capability tier and review policy.

## Human Checkpoints

Human review is required before or at:

- a consequential change to accepted product scope, architecture, or public contracts;
- a new production dependency, external service, persistence mechanism, or deployment unit;
- provider secret/security decisions and live-provider acceptance;
- DFHack command allowlists, process execution, or any uncertain read-only behavior;
- release approval and any waiver of a known limitation.

Human review is optional for bounded, reversible implementation with complete automated evidence and no unresolved decision. It is not required after every mechanical edit.

## Guardrails

- Routing is advisory capacity management, not permission to weaken validation, safety, or review.
- A mini-spec cannot override accepted decisions or silently reconcile conflicting authoritative documents.
- Vague work is clarified, routed upward, or blocked; it is not assigned downward because it looks small.
- Avoid one-file chores that lose observable value and vague mega-tasks that cross several ownership boundaries. Prefer one primary module plus necessary integration edges.
- Model names, usage limits, cost, latency, and availability are operational configuration and may change.
- Do not encode brittle pricing tables, temporary subscription quotas, or benchmark surveys in this ADR.
- Strongest-model/xhigh routing is reserved for ambiguity or risk that benefits from it, not routine implementation.

## Consequences

Positive:

- implementation tasks arrive with fewer architectural decisions left open;
- cheaper/faster models can safely complete precise work;
- review effort concentrates at consequential boundaries;
- backlog evidence and validation expectations become consistent across sessions.

Negative:

- mini-specs add planning and maintenance overhead;
- stale mini-specs can mislead unless checked against current authoritative documents;
- model availability can require route substitution;
- over-slicing can create coordination cost, while under-slicing preserves ambiguity.

The trade-off is accepted: a short, maintained execution contract is cheaper than repeated redesign or safety rework during implementation.
