# Observability Contract v0.1

Status: Draft  
Related ADR: `docs/decisions/adr-0004-observability.md`

## Goal

v0.1 observability must answer basic operational questions during local development without logging sensitive content.

## Signals

Use:

- structured logs,
- OpenTelemetry traces,
- OpenTelemetry metrics.

Logs alone are not sufficient.

## Required spans

```text
fortresssouls.dwarves.list
fortresssouls.dwarves.snapshot
fortresssouls.prompt.assemble
fortresssouls.llm.chat
fortresssouls.chat.turn
```

## Required span tags

```text
fortresssouls.adapter.type
fortresssouls.provider.type
fortresssouls.llm.model
fortresssouls.dwarf.id
fortresssouls.snapshot.schema_version
fortresssouls.prompt.template_version
fortresssouls.chat.session_id
```

Do not tag full prompt text by default.

## Required metrics

```text
fortresssouls.dwarves.list.duration
fortresssouls.dwarves.snapshot.duration
fortresssouls.prompt.tokens.estimated
fortresssouls.llm.request.duration
fortresssouls.llm.request.count
fortresssouls.llm.error.count
```

## Required structured log fields

```text
correlationId
sessionId
dwarfId
snapshotSchemaVersion
providerType
model
operation
durationMs
errorCode
```

Fields may be absent when not applicable.

## Correlation ID

Use `X-Correlation-ID`.

If a request does not include it, generate one.

Return the correlation ID in the response headers.

Include it in log scope and trace tags.

## Redaction

Never log:

- API keys,
- Authorization headers,
- full provider request headers,
- full prompts by default,
- full LLM responses by default,
- raw DFHack output by default,
- write-capable command text.

## Development diagnostics

Prompt preview may exist only in development mode.

Prompt preview must not include secrets.

Prompt preview is a product/debug endpoint, not telemetry.
