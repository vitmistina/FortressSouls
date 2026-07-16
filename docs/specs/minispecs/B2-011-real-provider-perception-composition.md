# B2-011: Wire Bounded Perception Through the Real LLM Provider

Status: Done
Parent backlog: `docs/backlog/v0.2-backlog.md#b2-011`
Product specification: `docs/specs/fortress-souls-v0.2.spec.md`
Primary module: LLM adapter composition with Application chat integration
Risk class: Critical
Recommended implementation model: Safety/architecture implementation tier (GPT-5.6 Sol)
Recommended reasoning level: High
Recommended review model: Independent safety/architecture review tier (GPT-5.6 Sol, high or max)
Human checkpoint: Required before live-provider acceptance

## Observable Outcome

With an OpenAI-compatible provider configured, a player can ask the selected
dwarf to look around and the application sends `look_around` as a structured
provider function, validates and executes the returned call through the active
read-only adapter, supplies the bounded observation to the model for a second
round, and returns final dwarf prose with a `look_around` success receipt.

The development prompt preview may identify the enabled tool and schema
versions, but it is not the callable tool surface. The provider request's
structured `tools` field is the callable surface.

## Why This Slice Exists

The repository already contains the application-owned agent contracts, closed
perception registry, fake agent journey, OpenAI-compatible tool-loop transport,
live read-only surroundings/stock services, and safe receipt UI. API composition
currently registers `IDwarfAgent` only when `llm.providerType = Fake`. With
`OpenAiCompatible`, `ChatSessionService` therefore has no agent, rejects the
perception route internally, and falls back to the v0.1 plain-chat provider
request with no structured tools.

This item closes that integration gap. It does not redesign the prompt, tools,
DFHack commands, chat API, or agent runtime.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B2-011
- `docs/architecture/0001-architecture-overview.md`
- `docs/specs/fortress-souls-v0.2.spec.md`
- `docs/specs/prompt-contract-v0.2.md`
- `docs/specs/perception-tools-v0.2.md`
- `docs/decisions/adr-0005-llm-provider-strategy.md`
- `docs/decisions/adr-0007-agent-runtime-and-tool-loop.md`
- `docs/research/r2-001-openrouter-tool-loop-live-proof-2026-06-22.md`
- `docs/runbooks/provider-configuration.md`
- `docs/runbooks/local-dev.md`
- `.agents/instructions/backend.instructions.md`
- `.agents/instructions/prompting.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/instructions/observability.instructions.md`
- `.agents/skills/modular-monolith-boundaries/SKILL.md`
- `.agents/skills/prompt-contracts/SKILL.md`
- `.agents/skills/observability-first/SKILL.md`
- current chat orchestration, LLM composition, tool-loop transport, perception
  registry, provider-status code, and their nearest tests

Conditional context:

- `.agents/instructions/dfhack.instructions.md` and
  `.agents/skills/dfhack-adapter-safety/SKILL.md` only if implementation would
  change a DFHack adapter, command, argument, or process boundary. Such a change
  is not expected for this item.

## Existing State To Inspect

Before editing, confirm the current worktree rather than assuming the planning
snapshot is unchanged. In the state that produced B2-011:

- `Program.cs` calls `AddFortressSoulsFakePerceptionAgent()` only for the Fake
  provider.
- `AddFortressSoulsLlm()` registers legacy `IChatProvider` implementations but
  does not compose the real-provider `IChatClient` and `IDwarfAgent` path.
- `ChatSessionService.TrySelectPerceptionRoute()` returns false whenever no
  `IDwarfAgent` is registered, causing a silent plain-chat fallback.
- `OpenAiCompatibleToolLoopChatClient` already maps structured functions,
  `tool_choice = auto`, assistant tool calls, tool results, bounds, and stable
  transport failures in isolated tests.
- The closed registry already maps tools to adapter-specific
  `ISurroundingsInspectionService` and `IStockInspectionService` implementations.

Preserve all pre-existing user changes. Add characterization coverage before
refactoring service registration.

## In Scope

- Separate perception registry/agent composition from the fake tool-loop client
  choice so both supported provider types can reuse the same application-owned
  closed registry.
- For Fake provider mode, preserve `FakeToolLoopChatClient` and deterministic
  fake-mode behavior.
- For OpenAI-compatible mode, compose `OpenAiCompatibleToolLoopChatClient` as
  the `IChatClient` used by `MicrosoftExtensionsAiDwarfAgent`.
- Preserve the existing legacy `IChatProvider` path for messages that do not
  select a perception route.
- Prove the real-provider perception path through a controlled HTTP handler or
  local test server with no public network and no real credential.
- Keep provider status and telemetry coherent for tool-loop calls using existing
  safe fields and stable outcomes; add no content-bearing diagnostics.
- Update provider/local-development documentation to state which provider and
  adapter combinations support bounded perception and how to recognize a safe
  receipt.

## Out Of Scope

- No new tool, tool argument, result field, prompt wording, prompt/schema version,
  route keyword, chat API field, provider, model picker, dependency, retry,
  streaming, persistence, background work, or multi-agent behavior.
- No change to DFHack command names, command allowlists, executable/path handling,
  Lua scripts, live result filtering, or read-only policy.
- No browser redesign; the existing safe receipt projection is sufficient.
- No requirement that CI call OpenRouter, another live provider, DFHack, or the
  public internet.
- No forced or provider-specific tool-choice policy change. If the configured
  model ignores the existing required-use instruction while receiving a valid
  tool definition, record that separately rather than silently changing the
  prompt or provider contract in this item.

## Boundaries And Invariants

- Application owns perception-route selection, session identity, execution
  policy, history atomicity, stable failures, and safe receipts.
- LLM owns `Microsoft.Extensions.AI` types, provider DTOs, HTTP serialization,
  function-call protocol mapping, and provider-specific exceptions.
- DwarfFortress owns fixed read-only DFHack execution and maps only validated,
  filtered application DTOs outward. The model never receives commands, paths,
  absolute coordinates, raw IDs beyond approved contracts, or raw DFHack data.
- API composition selects implementations but contains no routing, tool, or
  provider business rules.
- Only route-approved tools are supplied to a turn. Unknown or unapproved tool
  calls fail before application tool execution.
- Only player messages and final assistant prose persist. Tool calls, call IDs,
  arguments, observations, and intermediate messages remain turn-local.
- Failure or cancellation appends no partial history or success receipt.
- Prompt previews, logs, traces, metrics, status endpoints, and API errors never
  expose prompts, conversation, tool arguments/results, response bodies, secrets,
  authorization headers, paths, coordinates, names from observations, or raw
  exception text.

## Implementation Slices

### Slice 1: Characterize provider-specific composition

- Intended behavior: Fake and OpenAI-compatible provider modes each resolve
  exactly one appropriate `IChatClient`, one closed registry, and one
  `IDwarfAgent`; unsupported or duplicate registrations fail clearly.
- Likely files or modules touched: LLM service registration, API composition,
  DI/configuration tests.
- Test-first evidence: tests demonstrate that OpenAI-compatible mode currently
  lacks `IDwarfAgent`, while Fake mode resolves the existing deterministic path.
- Completion evidence: both modes resolve the expected tool-loop client without
  provider/framework types leaking outside LLM/API composition.

### Slice 2: Prove one real-provider `look_around` turn

- Intended behavior: an application/API chat turn matching the existing
  surroundings route sends one `look_around` function, accepts a controlled
  assistant tool call, executes the real closed registry against a deterministic
  read-only surroundings service, sends one tool-result message, and returns
  controlled final prose plus one safe receipt.
- Likely files or modules touched: LLM composition, controlled transport test
  support, chat integration tests.
- Test-first evidence: the integration test initially observes a one-round
  tool-less legacy request and no receipt.
- Completion evidence: the controlled transport observes two bounded requests;
  the first has only `look_around`, `tool_choice = auto`, and parallel calls
  disabled, while the second correlates the validated tool result without
  persisting protocol messages.

### Slice 3: Failure, status, telemetry, and documentation parity

- Intended behavior: invalid calls and provider/tool failures retain existing
  categories and atomicity, real-provider tool-loop calls update only approved
  provider/agent diagnostics, and local docs describe the proven behavior.
- Likely files or modules touched: provider-status integration, telemetry tests,
  provider/local-development runbooks.
- Test-first evidence: controlled malformed arguments, unknown tool, timeout,
  cancellation, transport failure, and sentinel redaction tests fail for any
  missing composition-specific behavior.
- Completion evidence: focused tests pass, no sensitive sentinel appears in
  diagnostics, and docs no longer imply that `DfHackProcess + OpenAiCompatible`
  perception works through prompt text alone.

## Acceptance Criteria

- [ ] OpenAI-compatible provider mode resolves the bounded dwarf agent and the
  existing closed perception registry through normal API startup composition.
- [ ] Fake provider mode and non-perception real-provider chat retain their
  existing observable behavior.
- [ ] A controlled end-to-end backend test proves the structured
  `look_around` request, validated execution, tool-result round trip, final
  prose, safe receipt, and history atomicity.
- [ ] The provider receives only the route-approved tool definitions in stable
  order; disabled and unknown tools cannot execute.
- [ ] Existing call/round/result/turn limits, cancellation, no-retry behavior,
  stable failures, provider status, and content-free telemetry remain enforced.
- [ ] No provider/framework DTO crosses the LLM boundary and no DFHack execution
  surface or command allowlist changes.
- [ ] Automated checks use a fake credential and controlled transport only;
  public network and live DFHack remain optional manual evidence.
- [ ] Provider and local-development runbooks explain the structured tool
  surface, supported composition, safe receipt, and manual-smoke limits.
- [ ] A human reviews provider request composition, secret handling, tool
  authorization, redaction, and any retained live-smoke evidence before marking
  B2-011 DONE.

## Test Strategy

Use red-green-refactor. Start with the smallest application/API composition test
that reproduces the missing real-provider agent. Then test the full two-request
function-call exchange through the real serializer and agent loop while replacing
only HTTP transport and read-only game observation boundaries.

Prefer existing test helpers and packages. Do not add a framework, snapshot every
provider byte, assert live prose, or weaken existing isolated tool-loop tests.
Use sentinel values to prove that prompts, player text, tool arguments/results,
provider bodies, credentials, paths, and raw exceptions do not enter telemetry,
status, errors, or receipts.

## Observability And Failure Behaviour

Preserve the existing `fortresssouls.chat.turn`, `fortresssouls.agent.turn`,
`fortresssouls.agent.tool.call`, and provider-call telemetry boundaries. Record
only approved stable IDs, provider/model, tool name, versions, ordinals, bounded
sizes, duration, outcome, and error category. Do not create a parallel status or
telemetry model for tool-enabled provider calls.

Expected provider, tool, timeout, cancellation, invalid-data, and budget failures
use the existing mappings. The browser must never receive raw provider or DFHack
errors, and a failed turn must not display a successful perception receipt.

## Validation

Focused checks during implementation:

```powershell
dotnet test .\src\backend\FortressSouls.Tests\FortressSouls.Tests.csproj --filter "OpenAiCompatibleToolLoop|PerceptionAgent|ChatSession"
dotnet test .\src\backend\FortressSouls.Tests\FortressSouls.Tests.csproj --filter "RuntimeStatus|AgentTelemetry|Architecture"
```

Final relevant checks:

```powershell
.\scripts\format.ps1
.\scripts\test.ps1
.\scripts\check.ps1
```

Optional manual smoke may use `DfHackProcess + OpenAiCompatible` only when a
human supplies approved local credentials and a loaded fortress. Record only
configuration mode, model, stable outcome, safe receipt, timing, and any bounded
error category. Do not retain request/response content, tool arguments/results,
coordinates, dwarf data, API keys, or authorization headers.

## Stop Conditions

- Stop if composition requires a new production dependency, agent framework,
  provider, deployment unit, generic tool dispatcher, or change to accepted
  module ownership.
- Stop if implementation would change prompt text/version, tool schema/version,
  tool-choice policy, route semantics, public API, or provider strategy; surface
  that as a separate contract decision.
- Stop if a DFHack command, allowlist, process argument, Lua script, or live
  filtering change appears necessary; B2-011 assumes those read-only boundaries
  already work.
- Stop if secure secret handling, bounded HTTP response reading, cancellation,
  failure atomicity, tool authorization, or redaction cannot be demonstrated.
- Stop before treating one model's refusal to call an available tool as license
  to add hidden prompt text, retry automatically, or claim a successful receipt.

## Completion Report

Report: 1. outcome and important design decisions; 2. changed files; 3. exact
validation commands and results; 4. known limitations, unverified assumptions,
and remaining manual checks; 5. mark B2-011 DONE in the backlog only after all
acceptance evidence and the required human checkpoint are complete.
