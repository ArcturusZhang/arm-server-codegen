#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cleans generated output and re-runs TypeSpec compilation.
.DESCRIPTION
    1. Rebuilds the server-code-emitter TypeScript project.
    2. Removes old Generated/ and swagger/ directories under typespec/.
    3. Runs `tsp compile .` to regenerate all emitter output.
#>

param(
    [switch]$SkipEmitterBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$emitterDir = Join-Path $repoRoot "server-code-emitter"
$typespecDir = Join-Path $repoRoot "typespec"

# Step 1: Rebuild the emitter
if (-not $SkipEmitterBuild) {
    Write-Host "==> Building server-code-emitter..." -ForegroundColor Cyan
    Push-Location $emitterDir
    try {
        npx tsc -p .
        if ($LASTEXITCODE -ne 0) { throw "Emitter build failed." }
        Write-Host "    Emitter build succeeded." -ForegroundColor Green
    } finally {
        Pop-Location
    }
} else {
    Write-Host "==> Skipping emitter build (-SkipEmitterBuild)." -ForegroundColor Yellow
}

# Step 2: Clean old output
Write-Host "==> Cleaning generated output..." -ForegroundColor Cyan
$dirsToClean = @("Generated", "swagger", "tsp-output")
foreach ($dir in $dirsToClean) {
    $path = Join-Path $typespecDir $dir
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path
        Write-Host "    Removed $dir/"
    }
}

# Step 3: Regenerate swagger (autorest emitter)
Write-Host "==> Running tsp compile (swagger)..." -ForegroundColor Cyan
Push-Location $typespecDir
try {
    npx tsp compile .
    if ($LASTEXITCODE -ne 0) { throw "TypeSpec compilation failed." }
    Write-Host "    Swagger generation succeeded." -ForegroundColor Green
} finally {
    Pop-Location
}

# Step 4: Run server-code-emitter for each version
$versions = @("2021-11-01", "2021-12-01", "2026-02-01")
foreach ($version in $versions) {
    $versionOutput = Join-Path $typespecDir "Generated" $version
    Write-Host "==> Analyzing version: $version" -ForegroundColor Cyan

    Push-Location $typespecDir
    try {
        npx tsp compile . `
            --emit "@azure-tools/typespec-server-emitter" `
            --option "@azure-tools/typespec-server-emitter.version=$version" `
            --option "@azure-tools/typespec-server-emitter.emitter-output-dir=$versionOutput"
        if ($LASTEXITCODE -ne 0) { throw "Server emitter failed for version $version" }
    } finally {
        Pop-Location
    }

    Write-Host ""
    Get-Content (Join-Path $versionOutput "impact-analysis.txt")
    Write-Host ""
}

Write-Host "==> Done!" -ForegroundColor Green
