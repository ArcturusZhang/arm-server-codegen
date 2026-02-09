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
$typespecDir = Join-Path $repoRoot "AzureSqlVersioningDemo" "typespec"

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

# Step 3: Regenerate
Write-Host "==> Running tsp compile..." -ForegroundColor Cyan
Push-Location $typespecDir
try {
    npx tsp compile .
    if ($LASTEXITCODE -ne 0) { throw "TypeSpec compilation failed." }
    Write-Host "==> Done!" -ForegroundColor Green
} finally {
    Pop-Location
}
