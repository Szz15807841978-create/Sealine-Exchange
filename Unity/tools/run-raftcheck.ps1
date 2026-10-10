# =============================================================================
#  Headless rule verification for the raft <-> ocean buoyancy solver.
#
#  Compiles Assets/Scripts/DivePoint/RaftBuoyancySolver.cs (pure logic) plus the
#  shared UnityEngine shim, then runs tools/RuleCheck/RaftRuleCheck.cs assertions.
#
#  Usage:
#      powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-raftcheck.ps1
#
#  NOTE: ASCII-only, same reason as run-rulecheck.ps1 / run-divecheck.ps1.
# =============================================================================
param(
    [string]$UnityEditor = 'E:\2022.3.62f3c1\Editor'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mb   = Join-Path $UnityEditor 'Data\MonoBleedingEdge'
$csc  = Join-Path $mb 'lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$mono = Join-Path $mb 'bin\mono.exe'

if (-not (Test-Path $csc))  { throw "Roslyn compiler not found: $csc" }
if (-not (Test-Path $mono)) { throw "mono not found: $mono" }

$outDir = Join-Path $env:TEMP 'whalefall-raftcheck'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$exe = Join-Path $outDir 'RaftRuleCheck.exe'

$dp = Join-Path $root 'Assets\Scripts\DivePoint'
$sources = @(
    (Join-Path $PSScriptRoot 'RuleCheck\UnityShim.cs'),
    (Join-Path $dp 'RaftBuoyancySolver.cs'),
    (Join-Path $dp 'RaftSkyPresets.cs'),
    (Join-Path $PSScriptRoot 'RuleCheck\RaftRuleCheck.cs')
)

foreach ($f in $sources) { if (-not (Test-Path $f)) { throw "Missing source: $f" } }

$rsp = Join-Path $outDir 'raftcheck.rsp'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-target:exe')
$lines.Add('-langversion:9.0')
$lines.Add('-out:"' + $exe + '"')
$lines.Add('-nowarn:0169,0414,0649,0219,0067')
foreach ($f in $sources) { $lines.Add('"' + $f + '"') }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

Write-Host "Compiling raft rule check..." -ForegroundColor Cyan
$build = & $mono $csc "@$rsp" 2>&1 | ForEach-Object { $_.ToString() }
$errs = @($build | Where-Object { $_ -match ': error ' })
if ($errs.Count -gt 0) {
    Write-Host "COMPILE FAILED:" -ForegroundColor Red
    $errs | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 2
}

Write-Host "Running assertions..." -ForegroundColor Cyan
Write-Host ""
& $mono $exe
$code = $LASTEXITCODE
Write-Host ""
if ($code -eq 0) { Write-Host "RAFT RULE CHECK: ALL PASSED" -ForegroundColor Green }
else { Write-Host "RAFT RULE CHECK: FAILURES PRESENT" -ForegroundColor Red }
exit $code
