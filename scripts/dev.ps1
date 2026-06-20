Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$BackendProject = Join-Path $RepoRoot "src\backend\FortressSouls.Api\FortressSouls.Api.csproj"
$FrontendDir = Join-Path $RepoRoot "src\frontend"

$backendProcess = $null

try {
    Write-Host "==> backend"
    $backendProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("run", "--launch-profile", "http", "--project", $BackendProject) `
        -WorkingDirectory $RepoRoot `
        -PassThru `
        -NoNewWindow

    if ($backendProcess.HasExited) {
        throw "backend exited before the frontend started with exit code $($backendProcess.ExitCode)."
    }

    Write-Host "==> frontend"
    Push-Location $FrontendDir
    try {
        & npm run dev -- --host 127.0.0.1 --strictPort
        if ($LASTEXITCODE -ne 0) {
            throw "frontend exited with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force
    }
}
