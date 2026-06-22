# R2-001: OpenRouter Tool-Loop Live Proof

**Status:** Completed retained evidence

**Date:** 2026-06-22

**Scope:** Live provider proof for `R2-001` and `ADR-0007` only. No v0.2
product, API, prompt-contract, or adapter-contract change is accepted by this
artifact on its own.

## Research outcome

- The configured OpenAI-compatible endpoint
  `https://openrouter.ai/api/v1/chat/completions` accepted the closed
  `probe_observe` function definition and returned one structured tool call.
- The current repository adapter path
  `MicrosoftExtensionsAiDwarfAgent -> OpenAiCompatibleToolLoopChatClient`
  also completed a live tool call and returned final prose when the initial
  instruction explicitly required a deterministic single call.
- The same adapter path did not call the tool when driven only by the generic
  probe description plus a normal player question. The observed model instead
  asked the player to clarify the tool arguments.
- Recommendation: accept `ADR-0007`. The live evidence supports the
  `Microsoft.Extensions.AI` path, and the negative result reinforces why the
  application must own prompt instructions, tool descriptions, limits, and
  stable fallback behavior instead of delegating policy to the model.

## Environment

Live evidence was captured from:

```text
Operating system: Windows (PowerShell)
dotnet SDK: 10.0.301
Endpoint: https://openrouter.ai/api/v1/chat/completions
Configured model: deepseek/deepseek-v3.2
Secret seam: FortressSouls__Llm__ApiKey loaded from local .env
Observed window: 2026-06-22T21:19+02:00 through 2026-06-22T21:23+02:00
```

The official package/support state recorded in `ADR-0007` was rechecked on
2026-06-22 against Microsoft Learn and NuGet with no material change from the
2026-06-21 snapshot already summarized there.

## Experiment 1: Raw endpoint proof

Method:

- Followed the manual two-request OpenRouter tool-call smoke in
  `docs/runbooks/provider-configuration.md`.
- Request 1 sent one system message, one user message, and one closed function
  definition for `probe_observe`.
- Request 2 replayed the returned tool call and supplied one deterministic tool
  result:

```json
{"schemaVersion":"probe.v1","summary":"ore bins ore bins"}
```

Observed result:

- Response 1 returned one `tool_calls` entry for `probe_observe`.
- The returned arguments were:

```json
{
  "subject": "ore bins",
  "repeatCount": 1,
  "emitLargePayload": false,
  "delayMs": 0
}
```

- Response 2 returned assistant prose grounded in the supplied tool result.
- No API key, Authorization header, or raw telemetry export was retained in the
  artifact.

Interpretation:

- The configured OpenRouter endpoint accepts the chat-completions tool-call
  wire shape needed by the spike.
- The live model selected a valid call without requiring custom provider
  protocol code in Fortress Souls.

## Experiment 2: Current adapter path with generic probe prompt

Method:

- Ran a temporary console harness that instantiated the current repository
  classes:
  `MicrosoftExtensionsAiDwarfAgent`,
  `OpenAiCompatibleToolLoopChatClient`,
  `ClosedAgentToolRegistry`,
  and `ProbeObservationToolService`.
- Used the same provider configuration as the repo:
  endpoint `https://openrouter.ai/api/v1`, model `deepseek/deepseek-v3.2`.
- Sent player message `What do you see near the ore bins?`
- Used the generic initial instruction:
  `Use the provided function when current structured data is needed. Reply briefly after tool use.`

Observed result:

- The model returned prose only and no tool receipt.
- The prose asked the player to clarify the tool arguments
  `subject`, `repeatCount`, `emitLargePayload`, and `delayMs`.

Interpretation:

- This is not a transport failure.
- It is a prompt/tool-description sensitivity finding for the current probe
  harness and current model.
- The result supports keeping tool policy and fallback behavior in the
  application rather than assuming the model will infer a useful call from a
  thin description.

## Experiment 3: Current adapter path with explicit deterministic instruction

Method:

- Reused the same temporary console harness and repository classes as
  Experiment 2.
- Changed only the initial instruction to explicitly require one call:

```text
Call probe_observe exactly once with subject='ore bins', repeatCount=1,
emitLargePayload=false, delayMs=0. After receiving the tool result, answer
briefly in prose and do not ask the player for clarification.
```

Observed result:

- The adapter completed one live tool call.
- The returned tool receipts were:

```json
[
  {
    "tool": "probe_observe",
    "outcome": "success"
  }
]
```

- The final assistant prose was grounded in the returned tool result and the
  turn completed successfully.

Interpretation:

- The current `Microsoft.Extensions.AI` adapter path can execute the full live
  tool loop against the configured endpoint.
- The remaining risk is prompt/tool-schema elicitation quality, not protocol
  compatibility.

## Decision impact

This evidence supports accepting `ADR-0007` with these retained conclusions:

- Keep the v0.2 tool loop on the smallest `Microsoft.Extensions.AI` path.
- Keep the boundary application-owned and framework-neutral.
- Keep whole-turn budgets, stable failures, safe receipts, and telemetry
  outside the library.
- Treat tool descriptions and tool-enabled prompt instructions as product
  contract inputs that need deliberate wording and test coverage.

This evidence does not justify:

- adopting Microsoft Agent Framework for the bounded v0.2 loop,
- moving provider DTOs or framework types into product contracts,
- assuming that a model will always choose a tool from a minimal description,
- weakening the bounded final-response path after budget or call exhaustion.

## Reproduction notes

Reproduce with:

1. The manual OpenRouter tool-call smoke in
   `docs/runbooks/provider-configuration.md`.
2. A temporary local harness that uses the repository's
   `OpenAiCompatibleToolLoopChatClient`,
   `MicrosoftExtensionsAiDwarfAgent`,
   and `ProbeObservationToolService`.

Do not commit API keys, Authorization headers, or full raw provider traces.
