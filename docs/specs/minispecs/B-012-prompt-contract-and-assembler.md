# B-012: Define prompt contract and prompt assembler

Status: Ready
Parent backlog: `docs/backlog/v0.1-backlog.md#b-012`
Product specification: `docs/specs/fortress-souls-v0.1.spec.md`
Primary module: Prompting
Risk class: High
Recommended implementation model: Prompt/contract implementation tier (GPT-5.5)
Recommended reasoning level: High
Recommended review model: Architecture-sensitive review tier (GPT-5.5 high)
Human checkpoint: Optional

## Observable Outcome

Given the same validated dwarf snapshot, bounded active conversation, static guide, and player message, the assembler returns byte-for-byte stable prompt text, an explicit template version, size diagnostics, and predictable over-budget failures.

## Why This Slice Exists

Application code, not the model, owns prompt structure, ordering, escaping, truncation, and input policy. It receives the validated snapshot bound to the browser-selected dwarf; it does not know about browser state or the DF UI cursor.

## Context To Load

Required context:

- `AGENTS.md`, this mini-spec, and B-012
- `docs/specs/fortress-souls-v0.1.spec.md`, section 9 and B-012
- completed B-008 snapshot contract
- `.agents/instructions/prompting.instructions.md`
- `.agents/instructions/testing.instructions.md`
- `.agents/skills/prompt-contracts/SKILL.md`
- `docs/specs/observability-v0.1.md`

Conditional context:

- `docs/research/dfhack-field-map.md`, section 6, for approved prompt candidates.
- B-005 instrumentation primitives when emitting the assembly span/metric.

## Existing State To Inspect

At planning time no Prompting project implementation or standalone prompt-contract file exists. Treat section 9 of the product spec plus root prompting instructions as the current contract; do not invent the missing `docs/specs/prompt-contract-v0.1.md` unless this item creates it as the canonical detailed contract.

## In Scope

- Create the concise canonical prompt contract and versioned deterministic assembler.
- Define validated `PromptInputs`, ordered sections, normalization/newline rules, escaping, history/message/string limits, and deterministic budget/truncation policy.
- Use only portrayal rules, validated prompt candidates for the browser-selected dwarf bound to the session, static interpretation guide, active session messages, and current player message.
- Return template version, estimated size/token count, truncation flags, and stable failure category without prompt content in diagnostics.
- Add focused unit tests and a reviewed golden prompt for a synthetic fixture.

## Out Of Scope

- No provider request, model prose, memory, tools, hidden fortress state, raw DFHack data, prompt repair, persistence, semantic tokenization dependency, or prompt preview endpoint.

## Boundaries And Invariants

- Identical normalized inputs produce identical output independent of culture, clock, random state, or serializer property order.
- Application policy owns ordering and truncation; the model never repairs malformed input.
- Input strings and collections are bounded and treated as data, not instructions.
- Secrets and raw/debug-only snapshot sections are excluded.
- Prompt text and conversation content never enter logs, tags, metrics, or exceptions.

## Implementation Slices

### Slice 1: Contract and deterministic assembly

- Intended behavior: valid inputs produce the exact versioned section order.
- Likely files or modules touched: Prompting contracts/assembler, prompt contract doc, unit/golden tests.
- Test-first evidence: golden and focused ordering/escaping tests fail first.
- Completion evidence: repeated/culture-varied runs produce identical output.

### Slice 2: Bounds, truncation, and diagnostics

- Intended behavior: oversized inputs follow one deterministic policy or stable rejection.
- Likely files or modules touched: budget policy, result diagnostics, failure tests, instrumentation.
- Test-first evidence: boundary tests for each limit and prohibited content fail first.
- Completion evidence: diagnostics are content-free and prompt size metric is emitted safely.

## Acceptance Criteria

- [ ] Prompt template and version are explicit and documented.
- [ ] Assembly is deterministic across repeated runs and culture settings.
- [ ] Only approved inputs appear, in the approved order.
- [ ] Size/history limits and truncation/rejection policy are deterministic and tested.
- [ ] Golden diff is reviewed and contains synthetic data only.
- [ ] No prompt, message, secret, hidden state, or raw DFHack content enters telemetry.

## Test Strategy

Write pure unit and golden tests first. Use real B-008 values and the real assembler; do not mock serialization, budget policy, or static guide. Include delimiter/instruction-like user text, empty optional fields, Unicode, oversize history, and cancellation where the public method supports it.

## Observability And Failure Behaviour

Create `fortresssouls.prompt.assemble` around assembly orchestration and record template version, estimated tokens/characters, truncation flag, outcome, and duration. Use a stable `PromptTooLarge`/validation error and never attach prompt or message content.

## Validation

```powershell
dotnet test .\src\backend\FortressSouls.sln --filter "Prompt"
.\scripts\format.ps1
.\scripts\test.ps1
```

Manually inspect the golden prompt and content-free telemetry assertions.

## Stop Conditions

- Stop if required input falls outside the validated session-bound dwarf state, active conversation, static guide, and player message.
- Stop if token-budget behavior requires a new production dependency or unapproved contract choice.
- Stop if B-008 cannot provide a validated prompt-candidate seam.

## Completion Report

Report: 1. outcome and design decisions; 2. changed files; 3. validation commands and results; 4. known limitations, unverified assumptions, and remaining manual checks.
