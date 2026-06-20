# B-016: Implement configurable real LLM provider and safe status API

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-016`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Llm adapter and configuration
Risk class: Critical
Recommended implementation model: Provider-security implementation tier (GPT-5.5)
Recommended reasoning level: High
Recommended review model: Critical boundary review tier (GPT-5.5 high)
Human checkpoint: Required

## Observable Outcome

When configured, one OpenAI-compatible provider sends bounded plain-chat requests to the accepted endpoint and returns prose; fake mode stays the default, failures are safe and stable, and a read-only status API exposes only allowlisted readiness/last-outcome metadata.

## Why This Slice Exists

This attaches the only v0.1 cloud boundary without allowing provider protocol, secrets, or probabilistic behavior to leak inward. ADR-0005 already decides OpenRouter/OpenAI-compatible and one default model, so implementation must not reopen provider strategy.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-016
- `docs/decisions/adr-0005-llm-provider-strategy.md`
- `docs/research/llm-provider-options.md`
- `docs/runbooks/provider-configuration.md`
- completed B-014 provider port, fake, and chat orchestration
- `docs/specs/observability-v0.1.md`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/observability.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/skills/observability-first/SKILL.md`

Conditional context:

- official endpoint documentation only if request/response details cannot be established from accepted research; record any consequential drift before changing the contract.

## Existing State To Inspect

At planning time no provider implementation exists. After dependencies, inspect `IChatProvider`, DI/configuration patterns, HTTP client policy, error contract, and telemetry helpers. Never inspect or print real local secret values.

## In Scope

- Implement `OpenAiCompatibleChatProvider` for the accepted chat-completions-shaped endpoint using framework HTTP facilities unless a dependency is already approved.
- Bind/validate only ProviderType, Endpoint, Model, ApiKey, MaxOutputTokens, Temperature, and TimeoutSeconds with safe defaults and bounds.
- Obtain secrets from environment/user secrets; keep Fake as test/offline default.
- Use structured HTTP requests, cancellation, one bounded timeout policy, response-size limit, strict success/JSON/content validation, and stable error mapping.
- Add an application-owned provider status projection/API containing only provider type, model, configured/readiness state, stable last outcome/error category, and bounded latency/time metadata.
- Ensure status reads are process-local, non-persistent, thread-safe, and trigger no provider call.
- Add redaction-safe telemetry and deterministic component tests with a controlled HTTP handler/server.
- Keep the runbook synchronized without live credentials.

## Out Of Scope

- No streaming, tools, retries unless an accepted bounded policy already exists, provider marketplace, model picker/editor, secret editor, direct OpenAI Responses API, persistence, content logging, status network probe, frontend diagnostics UI, or live-network CI test.

## Boundaries And Invariants

- Provider DTOs/exceptions remain in Llm; Application sees only its port contracts.
- Endpoint configuration is validated HTTPS for real cloud mode; no arbitrary browser-supplied URL or headers.
- API key/Authorization, prompt, response, headers, and raw provider body never enter logs, traces, errors, health, diagnostics UI, or test snapshots.
- Input/output and timeout are bounded; cancellation propagates.
- Provider prose is untrusted text and remains data at all later boundaries.

## Implementation Slices

### Slice 1: Configuration and request mapping

- Intended behavior: valid config creates the exact bounded HTTP request; invalid/missing config fails before network.
- Likely files or modules touched: Llm options/adapter, DI registration, mapping tests.
- Test-first evidence: table tests for missing key, invalid endpoint/model/limits, and request body fail first.
- Completion evidence: controlled handler observes expected URI/headers/JSON without secret output.

### Slice 2: Response/failure handling

- Intended behavior: success returns plain text; timeout/cancel/status/oversize/malformed/empty responses map safely.
- Likely files or modules touched: adapter parser/error mapping and component tests.
- Test-first evidence: each failure-path test fails before handling.
- Completion evidence: all stable categories and cancellation semantics pass.

### Slice 3: Composition, safe status, telemetry, and runbook

- Intended behavior: configured real mode works, fake remains default, status reads perform no network work, and telemetry is content-free.
- Likely files or modules touched: Application status projection, API endpoint/composition, telemetry tests, provider runbook.
- Test-first evidence: registration/default/status/no-network/redaction tests fail first.
- Completion evidence: allowlisted status contract and automated suite pass; optional live smoke is recorded separately.

## Acceptance Criteria

- [ ] Implementation follows ADR-0005 without adding provider/model selection features.
- [ ] Fake remains default for tests and offline development.
- [ ] Configuration and all request/response sizes/timeouts are validated and bounded.
- [ ] Missing key, cancellation, timeout, non-success, malformed/empty, and oversized responses map to safe stable errors.
- [ ] Status API exposes only allowlisted readiness/last-outcome fields, performs no network request, and returns no endpoint/key/header/raw error/content.
- [ ] Tests use no public network or real secret and verify no sensitive telemetry.
- [ ] Human reviews request shape, secret handling, endpoint validation, and error mapping before acceptance.

## Test Strategy

Write configuration, mapping, and failure component tests first against a controlled HTTP boundary. Use real JSON serialization, real `HttpClient`, and the real adapter; substitute only transport. Never assert exact live prose or require external network in automated checks.

## Observability And Failure Behaviour

`fortresssouls.llm.chat` records provider type, model, outcome, request duration, bounded usage/size counts, request/error counters, and stable error code. It never records endpoint, headers, prompt, response, raw body, or exception message. User-visible errors distinguish configuration, timeout, unavailable, invalid response, and cancelled without provider internals.

## Validation

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "OpenAiCompatible|ProviderConfiguration|Redaction"
.\scripts\format.ps1
.\scripts\test.ps1
```

Manual verification is optional and only when a human supplies credentials through approved local secret configuration. Record outcome/category and latency, never content or key.

## Stop Conditions

- Stop for human decision if accepted endpoint/request shape has materially drifted or a new SDK/dependency is necessary.
- Stop if secure endpoint validation, bounded response reading, or secret redaction cannot be demonstrated.
- Stop before adding retry, streaming, tools, provider marketplace, or diagnostics that expose content.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
