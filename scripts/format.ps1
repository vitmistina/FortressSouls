Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$BackendSolution = Join-Path $RepoRoot "src\backend\FortressSouls.slnx"
$FrontendDir = Join-Path $RepoRoot "src\frontend"

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter()]
        [string[]]$Arguments = @(),

        [Parameter()]
        [string]$WorkingDirectory = $RepoRoot
    )

    Write-Host "==> $Label"
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Label failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

Invoke-NativeCommand -Label "backend format" -FilePath "dotnet" -Arguments @("format", $BackendSolution, "--verify-no-changes")
Invoke-NativeCommand -Label "frontend lint" -FilePath "npm" -Arguments @("run", "lint") -WorkingDirectory $FrontendDir
