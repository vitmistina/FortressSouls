Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "format.ps1")
& (Join-Path $PSScriptRoot "test.ps1")
