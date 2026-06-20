# B-014: Implement fake-provider in-memory chat backend

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-014`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Application chat orchestration, Llm fake, and API
Risk class: High
Recommended implementation model: Cross-module integration tier (GPT-5.5)
Recommended reasoning level: High
Recommended review model: Architecture/prompt review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

A caller creates an in-memory session for a dwarf ID selected from the backend list, sends bounded messages, receives deterministic fake-provider replies, and can inspect a development-only prompt preview; all state disappears on restart.

## Why This Slice Exists

B-013 is absorbed here because a provider interface and fake have no observable value until chat orchestration invokes them. The slice completes one backend turn across the deterministic prompt boundary and the fake probabilistic boundary.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-013/B-014 backlog rows
- completed B-009 dwarf API/port and B-012 prompt assembler
- `docs/decisions/adr-0002-modular-monolith.md`
- `docs/decisions/adr-0005-llm-provider-strategy.md`
- `docs/specs/fortress-souls-v0.1.spec.md`, chat API sections 8.4-8.6
- `docs/specs/observability-v0.1.md`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/prompting.instructions.md`
- `.agents/instructions/observability.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/skills/prompt-contracts/SKILL.md`
- `.agents/skills/observability-first/SKILL.md`

Conditional context:

- B-009 public error conventions and the actual B-012 prompt budget/result types.

## Existing State To Inspect

Inspect actual Application ports, dwarf lookup behavior, prompt limits, API error shape, correlation primitives, DI conventions, and project references. No provider code or session store exists at planning time.

## In Scope

- Define a minimal application-owned `IChatProvider` for one bounded plain-text prose call with cancellation, diagnostics, and stable failures.
- Implement deterministic `FakeChatProvider` with no HTTP, filesystem, process, clock, random, or external dependency; keep it the default for tests/offline mode.
- Implement a bounded process-local session store with explicit maximum sessions, message/history limits, and deterministic eviction/truncation policy.
- Create sessions from a validated `dwarfId`; fetch and bind the matching snapshot so history can never cross dwarf identities.
- Implement create-session, send-message, and Development-only prompt-preview endpoints.
- Orchestrate validation -> snapshot -> prompt -> fake provider -> atomic history update with cancellation and failure safety.
- Add lifecycle, restart/non-persistence, bounds, identity, concurrency, provider, preview-gating, failure-atomicity, and nested-telemetry tests.

## Out Of Scope

- No real provider, persistence, streaming, tools, agent runtime, background work, multi-dwarf session, retry engine, or model access to DFHack/filesystem/shell.

## Boundaries And Invariants

- Browser selection supplies only a dwarf ID previously returned by the list API; session creation validates and binds it server-side.
- Switching browser selection creates or explicitly resets to a new session; history never migrates between dwarves.
- Application owns sessions/orchestration and the provider port; provider DTOs remain in Llm; API remains transport-only.
- Browser input, history, adapter data, and provider prose are untrusted and bounded.
- Failed/cancelled turns do not append partial assistant history.
- Prompt preview exists only in Development and is a response endpoint, never telemetry.

## Implementation Slices

### Slice 1: Provider port and deterministic fake

- Intended behavior: Application can invoke one plain prose call and receive stable fake diagnostics.
- Likely files or modules touched: Application provider contracts, Llm/Test support fake, DI, architecture/contract tests.
- Test-first evidence: determinism, cancellation, bounds, no-I/O, and dependency tests fail first.
- Completion evidence: Fake is the offline default and no provider protocol leaks inward.

### Slice 2: Session lifecycle and chat turn

- Intended behavior: create/send uses the listed dwarf's validated snapshot and updates bounded history atomically.
- Likely files or modules touched: Application session store/use cases, API DTOs/endpoints, integration tests.
- Test-first evidence: lifecycle, identity, limits, restart, concurrent turn, provider failure, and cancellation tests fail first.
- Completion evidence: real prompt assembler plus fake provider produces the documented response/history.

### Slice 3: Preview and telemetry

- Intended behavior: preview is Development-only and chat/prompt/provider spans are nested and content-free.
- Likely files or modules touched: preview endpoint/environment gate and telemetry tests.
- Test-first evidence: production absence and sentinel-content redaction tests fail first.
- Completion evidence: preview/telemetry contract passes without content leakage.

## Acceptance Criteria

- [ ] Provider port is minimal, plain-text, bounded, cancellation-aware, and free of tools/streaming/memory.
- [ ] Fake provider is deterministic, no-I/O, and default for tests/offline mode.
- [ ] Session creation validates a browser-selected listed dwarf ID and binds the matching snapshot.
- [ ] Sessions/history are bounded, in-memory only, and absent after restart.
- [ ] Failed, cancelled, or concurrent turns follow an explicit tested atomic policy.
- [ ] Preview is Development-only and excludes secrets/raw DFHack data.
- [ ] Integration tests cover create/send/preview and content-free nested spans.

## Test Strategy

Write provider contract tests, then API integration tests using the real store, fake dwarf adapter, real assembler, and fake provider. Replace true boundaries only to force specific failures. Use synchronization primitives rather than sleeps for concurrency tests.

## Observability And Failure Behaviour

Nest `fortresssouls.prompt.assemble` and `fortresssouls.llm.chat` under `fortresssouls.chat.turn`. Record safe IDs, versions, provider/model, sizes, outcome, duration, and stable error codes; never record prompt, message, response, dwarf name, or raw error text.

## Validation

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "ChatProvider|FakeChatProvider|ChatSession|ChatTurn|PromptPreview"
.\scripts\format.ps1
.\scripts\test.ps1
```

Manually verify fake create/send/preview in Development and preview absence outside Development.

## Stop Conditions

- Stop if session identity or concurrent-turn policy remains ambiguous.
- Stop if implementation starts adding persistence, tools, streaming, retries, or provider protocol to Application.
- Stop if preview or telemetry would expose prohibited content.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
