#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the server-code-emitter impact analysis for each API version defined in the test TypeSpec.
#>

$ErrorActionPreference = "Stop"

$testDir = $PSScriptRoot
$versions = @("2024-01-01", "2024-06-01", "2025-01-01")
$outputBase = Join-Path $testDir "test-output"

# Clean previous output
if (Test-Path $outputBase) {
    Remove-Item -Recurse -Force $outputBase
}

foreach ($version in $versions) {
    $versionOutput = Join-Path $outputBase $version
    Write-Host "==> Analyzing version: $version" -ForegroundColor Cyan

    Push-Location $testDir
    try {
        npx tsp compile test.tsp `
            --option "@azure-tools/typespec-server-emitter.version=$version" `
            --option "@azure-tools/typespec-server-emitter.emitter-output-dir=$versionOutput"
        if ($LASTEXITCODE -ne 0) { throw "tsp compile failed for version $version" }
    } finally {
        Pop-Location
    }

    Write-Host ""
    Get-Content (Join-Path $versionOutput "impact-analysis.txt")
    Write-Host ""
}

Write-Host "==> All versions analyzed." -ForegroundColor Green
