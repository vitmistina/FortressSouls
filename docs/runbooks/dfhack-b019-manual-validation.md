# DFHack B-019 manual validation runbook

## Purpose

Validate the two read-only DFHack scripts used by Fortress Souls v0.1:

```text
fortress-souls/list-dwarves
fortress-souls/get-dwarf-snapshot
```

and verify the backend DFHack process adapter status projection:

```text
GET /api/dwarves/adapter-status
```

This is optional manual evidence for live mode. Fake mode remains the default
supported development path.

## Assumptions

- Dwarf Fortress is running.
- DFHack is running.
- A fortress map is loaded.
- Validated scripts are stored in the repo under:

```text
dfhack/scripts/fortress-souls/
```

- Canonical retained samples are stored in:

```text
dfhack/samples/
```

- Runtime scripts are installed under:

```text
C:\Program Files (x86)\Steam\steamapps\common\DFHack\hack\scripts\fortress-souls\
```

## Manual script preparation

Live DFHack validation is optional and manual. Before running the commands
below, copy the validated repo scripts from:

```text
dfhack/scripts/fortress-souls/
```

into the DFHack runtime scripts directory:

```text
C:\Program Files (x86)\Steam\steamapps\common\DFHack\hack\scripts\fortress-souls\
```

Example PowerShell:

```powershell
# Run from the repository root.
$repoScripts = Join-Path (Get-Location).Path "dfhack\scripts\fortress-souls\*"
$dfhackRuntime = "C:\Program Files (x86)\Steam\steamapps\common\DFHack\hack\scripts\fortress-souls\"

Copy-Item -Path $repoScripts -Destination $dfhackRuntime -Force
```

This is a manual preparation step for optional live-mode validation, not a
turnkey setup flow. `scripts/import-dfhack-scripts.ps1` is the maintainer
sync-back helper that copies from the DFHack install into this repo; it is not
the install step for live validation.

## Validate dwarf list

```powershell
cd "C:\Program Files (x86)\Steam\steamapps\common\DFHack\hack"

$out = "$env:TEMP\fortress-souls-list.json"
$err = "$env:TEMP\fortress-souls-list.err.txt"

.\dfhack-run.exe fortress-souls/list-dwarves > $out 2> $err
$exit = $LASTEXITCODE

"EXIT=$exit"
Get-Content $out -Raw | ConvertFrom-Json | Select-Object schemaVersion, count, mapLoaded, siteLoaded, worldLoaded
Get-Content $err -Raw
```

Expected:

```text
EXIT=0
schemaVersion = fortress-souls-dwarf-list.v0.1
count > 0
```

## Validate selected dwarf snapshot

Replace `6597` with a valid unit id from the list.

```powershell
cd "C:\Program Files (x86)\Steam\steamapps\common\DFHack\hack"

$unitId = "6597"
$out = "$env:TEMP\fortress-souls-snapshot-$unitId.json"
$err = "$env:TEMP\fortress-souls-snapshot-$unitId.err.txt"

.\dfhack-run.exe fortress-souls/get-dwarf-snapshot $unitId > $out 2> $err
$exit = $LASTEXITCODE

"EXIT=$exit"

$snapshot = Get-Content $out -Raw | ConvertFrom-Json

$snapshot |
  Select-Object schemaVersion, requestedUnitId, soulPresent, mapLoaded, siteLoaded, worldLoaded |
  Format-List

$snapshot.identity | Format-List
$snapshot.promptCandidates.topSkills | Select-Object -First 5 token, effective, totalExperience | Format-Table -AutoSize
$snapshot.promptCandidates.extremeTraits | Select-Object -First 8 token, value, deviationFromNeutral50, polarity | Format-Table -AutoSize

Get-Content $err -Raw
```

Expected:

```text
EXIT=0
schemaVersion = fortress-souls-dwarf-snapshot.v0.1
soulPresent = True
```

## Failure handling

If `ConvertFrom-Json` fails, inspect raw stdout:

```powershell
Get-Content $out -Raw
```

DFHack/Lua stack traces can appear on stdout. The backend must treat stdout as untrusted until JSON parsing succeeds.

## Validate backend DFHack adapter status projection

Configure backend `FortressSouls:DfHack` options for the local DFHack install:

```json
{
  "FortressSouls": {
    "DfHack": {
      "Enabled": true,
      "RunPath": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\DFHack\\hack\\dfhack-run.exe",
      "WorkingDirectory": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\DFHack\\hack",
      "Host": "127.0.0.1",
      "Port": 5000,
      "TimeoutMs": 3000
    }
  }
}
```

Then query:

```powershell
Invoke-RestMethod http://localhost:5230/api/dwarves/adapter-status
```

Expected:

- `adapterType = DfHackProcess`
- status read performs no process launch and no TCP preflight
- last outcome/error category update only after list/snapshot operations

## Validated run

The B-019 validation run on 2026-06-19 produced:

```text
ListCount            : 7
ValidSnapshotCount   : 7
ErrorSnapshotCount   : 0
InvalidSnapshotCount : 0
```

Maintainer utilities related to this run:

```text
scripts/import-dfhack-scripts.ps1
scripts/validate-dfhack-samples.ps1
```
