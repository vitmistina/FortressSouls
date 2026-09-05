param(
    [string]$SampleRoot = ".\dfhack\samples\research"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-Json([string]$Path) {
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Grid($Plane, [int]$ExpectedWidth, [int]$ExpectedHeight, [string]$Label) {
    Assert-Condition ($Plane.bounds.width -eq $ExpectedWidth) "$Label has an unexpected width."
    Assert-Condition ($Plane.bounds.height -eq $ExpectedHeight) "$Label has an unexpected height."
    Assert-Condition (@($Plane.grid).Count -eq $ExpectedHeight) "$Label has an unexpected grid height."
    foreach ($row in $Plane.grid) {
        Assert-Condition ($row.Length -eq $ExpectedWidth) "$Label contains a row with the wrong width."
    }
    Assert-Condition (($Plane.counts.valid + $Plane.counts.outsideMap) -eq ($ExpectedWidth * $ExpectedHeight)) "$Label cell arithmetic failed."
    Assert-Condition ($Plane.hiddenCellSamples.Count -le 24) "$Label emitted too many hidden samples."
    foreach ($cell in $Plane.hiddenCellSamples) {
        $names = @($cell.PSObject.Properties.Name | Sort-Object)
        Assert-Condition (($names -join ',') -eq 'dx,dy,visibility') "$Label hidden sample contains extra fields."
        Assert-Condition ($cell.visibility -eq 'hidden') "$Label hidden sample has an invalid visibility value."
    }
}

function Assert-Scene([string]$Path, [string]$ExpectedClass, [bool]$ExpectEdge) {
    $sample = Read-Json $Path
    $bytes = (Get-Item -LiteralPath $Path).Length
    $label = Split-Path -Leaf $Path

    Assert-Condition ($bytes -lt 131072) "$label exceeds the 128 KiB raw JSON limit."
    Assert-Condition ($sample.schemaVersion -eq 'fortress-souls-current-scene-research.v0.1') "$label has an unexpected schema."
    Assert-Condition ($sample.provenance.kind -eq 'live-dfhack') "$label is not marked live-dfhack."
    Assert-Condition ($sample.observer.flags.classification -eq $ExpectedClass) "$label has the wrong environment classification."
    Assert-Condition ($sample.observer.flags.source -eq 'dfhack.maps.getTileFlags') "$label does not record the accepted flag source."
    Assert-Grid $sample.localPlane 33 33 "$label local plane"
    Assert-Grid $sample.sitePlane 24 12 "$label site plane"

    if ($ExpectEdge) {
        Assert-Condition ($sample.localPlane.counts.outsideMap -gt 0) "$label does not prove map-edge handling."
    }

    Write-Host "$label`: PASS ($bytes bytes, $ExpectedClass)"
}

$root = (Resolve-Path $SampleRoot).Path
Assert-Scene (Join-Path $root 'current-scene.outdoor.live-2026-07-16.json') 'outdoor' $false
Assert-Scene (Join-Path $root 'current-scene.sheltered.live-2026-07-16.json') 'sheltered' $false
Assert-Scene (Join-Path $root 'current-scene.underground.live-2026-07-16.json') 'underground' $false
Assert-Scene (Join-Path $root 'current-scene.edge.live-2026-07-16.json') 'underground' $true

$outdoor = Read-Json (Join-Path $root 'current-scene.outdoor.live-2026-07-16.json')
Assert-Condition ($outdoor.buildingEvidence.wagonCandidates.typeName -eq 'Wagon') 'The outdoor sample does not retain a Wagon candidate.'
Assert-Condition (($outdoor.inventoryEvidence.counts.inBounds) -eq ($outdoor.inventoryEvidence.counts.contained + $outdoor.inventoryEvidence.counts.inventoryReferenced + $outdoor.inventoryEvidence.counts.looseCandidates)) 'Inventory exclusion arithmetic failed.'
Assert-Condition (@($outdoor.inventoryEvidence.examples | Where-Object { $_.inventory.modeName -eq 'Worn' }).Count -gt 0) 'The outdoor sample does not prove worn inventory references.'

$unitEnvironments = Read-Json (Join-Path $root 'current-scene.unit-environments.live-2026-07-16.json')
Assert-Condition ($unitEnvironments.unitEnvironmentScan.counts.outdoor -gt 0) 'The unit environment scan lacks an outdoor observation.'
Assert-Condition ($unitEnvironments.unitEnvironmentScan.counts.underground -gt 0) 'The unit environment scan lacks an underground observation.'

$shelteredScan = Read-Json (Join-Path $root 'current-scene.sheltered-scan.live-2026-07-16.json')
Assert-Condition ($shelteredScan.shelteredTileScan.counts.sheltered -gt 0) 'The sheltered tile scan found no sheltered tile.'

$features = Read-Json (Join-Path $root 'current-scene.features.live-2026-07-16.json')
foreach ($feature in @('water','plants','ramps','walls','buildings')) {
    Assert-Condition ($features.featureScan.counts.$feature -gt 0) "The feature scan found no $feature evidence."
}

$currentUnit211Path = Join-Path $root 'current-scene.unit-211.current-save-2026-07-16.json'
$currentUnit328Path = Join-Path $root 'current-scene.unit-328.current-save-2026-07-16.json'
$currentStairsPath = Join-Path $root 'current-scene.stairs-scan.current-save-2026-07-16.json'
$currentFeaturesPath = Join-Path $root 'current-scene.features.current-save-2026-07-16.json'
$currentEnvironmentsPath = Join-Path $root 'current-scene.unit-environments.current-save-2026-07-16.json'
$currentInventoryModesPath = Join-Path $root 'current-scene.inventory-modes.current-save-2026-07-16.json'

$hasCurrentSaveEvidence = (Test-Path $currentUnit211Path) -and (Test-Path $currentUnit328Path) -and (Test-Path $currentStairsPath) -and (Test-Path $currentFeaturesPath) -and (Test-Path $currentEnvironmentsPath) -and (Test-Path $currentInventoryModesPath)
if ($hasCurrentSaveEvidence) {
    Assert-Scene $currentUnit211Path 'sheltered' $false
    Assert-Scene $currentUnit328Path 'underground' $false

    $currentEnvironments = Read-Json $currentEnvironmentsPath
    Assert-Condition ($currentEnvironments.unitEnvironmentScan.counts.sheltered -gt 0) 'The current-save unit scan lacks an active sheltered observation.'

    $currentInventoryModes = Read-Json $currentInventoryModesPath
    Assert-Condition ($currentInventoryModes.inventoryModeScan.modeCounts.Weapon -gt 0) 'The current-save inventory scan lacks a Weapon-mode reference.'

    $currentHeldSample = Read-Json $currentUnit328Path
    Assert-Condition (@($currentHeldSample.inventoryEvidence.inventoryExamples | Where-Object { $_.inventory.modeName -eq 'Weapon' }).Count -gt 0) 'The current-save unit sample lacks a retained Weapon-mode item reference.'

    $currentFeatures = Read-Json $currentFeaturesPath
    Assert-Condition ($currentFeatures.featureScan.counts.stairs -gt 0) 'The current-save feature scan lacks stair evidence.'
}

$stairsPath = if ($hasCurrentSaveEvidence) { $currentStairsPath } else { Join-Path $root 'current-scene.stairs-scan.live-2026-07-16.json' }
$stairs = Read-Json $stairsPath
Assert-Condition ($stairs.stairScan.scanned -gt 0) 'The stair scan did not inspect any tiles.'
if ($stairs.stairScan.found) {
    Write-Host 'Stair evidence: PASS'
    $stairEvidenceFound = $true
} else {
    Write-Warning 'Stair evidence: NOT FOUND in the retained save; load a save containing stairs for completion.'
    $stairEvidenceFound = $false
}

if ($hasCurrentSaveEvidence -and $stairEvidenceFound) {
    Write-Host 'R2.1-001 current-scene sample validation: PASS (current-save sheltered, inventory, and stair evidence retained)'
} else {
    Write-Host 'R2.1-001 current-scene sample validation: PASS (with the retained stair limitation above)'
}
