<#
.SYNOPSIS
  Runs every quality gate, in order, and stops at the first failure.
  The same script runs locally (Windows PowerShell 5.1 or pwsh 7) and in CI, so the two can't drift apart.

.EXAMPLE
  ./scripts/verify.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'UrlShortener.slnx'
$results = Join-Path $root 'TestResults'

function Invoke-Gate {
    param([string]$Name, [scriptblock]$Command)
    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $Name (exit code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "OK: $Name" -ForegroundColor Green
}

Invoke-Gate 'Restore' { dotnet restore $solution --nologo }

Invoke-Gate 'Build (warnings are errors, analyzers on)' {
    dotnet build $solution -c $Configuration --no-restore --nologo
}

Invoke-Gate 'Format check' { dotnet format $solution --verify-no-changes --no-restore }

Invoke-Gate 'Tests with coverage' {
    if (Test-Path $results) { Remove-Item $results -Recurse -Force }
    dotnet test $solution -c $Configuration --no-build --nologo `
        --collect 'XPlat Code Coverage' --results-directory $results
}

Invoke-Gate 'Vulnerable packages (direct and transitive)' {
    $output = dotnet list $solution package --vulnerable --include-transitive 2>&1 | Out-String
    Write-Host $output
    # 'dotnet list package' exits 0 even when it finds vulnerabilities, so check the text.
    if ($output -match 'has the following vulnerable packages') {
        Write-Host 'Vulnerable packages found.' -ForegroundColor Red
        $global:LASTEXITCODE = 1
    }
}

Write-Host ""
Write-Host 'All quality gates passed.' -ForegroundColor Green
