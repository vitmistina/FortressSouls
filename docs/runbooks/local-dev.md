# Local Development Runbook

Status: Draft  
Applies to: FortressSouls v0.1  
Related ADR: `docs/decisions/adr-0004-observability.md`

## Purpose

Make local startup repeatable without turning the modular monolith into a petting zoo of tiny services.

v0.1 local development has three useful modes:

1. backend only,
2. backend plus frontend,
3. backend plus frontend plus local telemetry dashboard.

## Prerequisites

Required:

- .NET SDK matching the project target,
- Node.js for the frontend,
- PowerShell on Windows.

Optional:

- Docker Desktop, if using the Aspire Dashboard container,
- Aspire CLI, if using `npx -y @microsoft/aspire-cli dashboard run`.

## Environment defaults

For local fake mode:

```powershell
$env:FortressSouls__DwarfFortress__AdapterType = "Fake"
$env:FortressSouls__Llm__ProviderType = "Fake"
```

For local telemetry to Aspire Dashboard:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
$env:OTEL_SERVICE_NAME = "FortressSouls.Api"
```

Telemetry export must never be required for the app to start.

## Start Aspire Dashboard

### Option A: Aspire CLI

```powershell
npx -y @microsoft/aspire-cli dashboard run --allow-anonymous
```

Then open:

```text
http://localhost:18888
```

The dashboard receives OTLP/gRPC on:

```text
http://localhost:4317
```

and OTLP/HTTP on:

```text
http://localhost:4318
```

### Option B: Docker

```powershell
docker run --rm -it `
  -p 18888:18888 `
  -p 4317:18889 `
  -p 4318:18890 `
  -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
  --name aspire-dashboard `
  mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

Then open:

```text
http://localhost:18888
```

## Start backend

From the repository root:

```powershell
dotnet run --project .\src\backend\FortressSouls.Api\FortressSouls.Api.csproj
```

Expected health endpoint:

```text
GET http://localhost:<api-port>/api/health
```

Expected response shape:

```json
{
  "status": "ok",
  "version": "0.1.0",
  "adapter": "Fake",
  "provider": "Fake"
}
```

## Start frontend

From the repository root:

```powershell
cd .\src\frontend
npm install
npm run dev
```

The frontend should show:

- backend health status,
- placeholder or real dwarf list depending on implementation phase,
- diagnostics panel.

## Recommended scripts

When scripts are added, prefer bulk commands over manual bead-threading.

Suggested files:

```text
scripts/dev.ps1
scripts/test.ps1
scripts/start-dashboard.ps1
```

### `scripts/start-dashboard.ps1`

Suggested content:

```powershell
docker rm -f aspire-dashboard 2>$null

docker run --rm -it `
  -p 18888:18888 `
  -p 4317:18889 `
  -p 4318:18890 `
  -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
  --name aspire-dashboard `
  mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

### `scripts/test.ps1`

Suggested behaviour:

```text
dotnet test src/backend/FortressSouls.sln
cd src/frontend
npm test
npm run build
```

Only include frontend commands after the frontend exists.

## What to verify in the dashboard

After calling `/api/health`, verify:

- a trace exists for the HTTP request,
- structured logs include `correlationId`,
- service name appears as `FortressSouls.Api` or equivalent,
- no secrets appear in logs,
- no full prompt appears in logs.

After dwarf endpoints exist, verify spans:

```text
fortresssouls.dwarves.list
fortresssouls.dwarves.snapshot
```

After chat exists, verify spans:

```text
fortresssouls.chat.turn
fortresssouls.prompt.assemble
fortresssouls.llm.chat
```

## Troubleshooting

### Dashboard is empty

Check:

```powershell
echo $env:OTEL_EXPORTER_OTLP_ENDPOINT
echo $env:OTEL_EXPORTER_OTLP_PROTOCOL
```

Expected:

```text
http://localhost:4317
grpc
```

Also check that the API was started after these environment variables were set.

### Backend fails when dashboard is not running

This is a bug.

Telemetry export must be optional in local dev. The app must run in fake mode without the dashboard.

### Logs contain prompt text

This is a bug unless the developer explicitly used a prompt-preview endpoint.

Default telemetry must not include full prompts or full model responses.

### Logs contain API key or Authorization header

Stop immediately. Treat this as a safety defect.

## Later optional path: Aspire AppHost

B-006 may add:

```text
src/backend/FortressSouls.AppHost
src/backend/FortressSouls.ServiceDefaults
```

This is acceptable if it makes one-command startup easier.

It must not:

- introduce microservices,
- require cloud resources,
- make the dashboard mandatory,
- hide the basic `dotnet run` path.
