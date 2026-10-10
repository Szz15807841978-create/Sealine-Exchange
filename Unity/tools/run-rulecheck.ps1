# =============================================================================
#  Headless rule verification for the Unity port of "Fishing".
#
#  Uses the Roslyn compiler and mono bundled with Unity -- no .NET SDK needed.
#
#  Usage:
#      powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-rulecheck.ps1
#      powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-rulecheck.ps1 `
#                 -UnityEditor 'E:\2022.3.62f3c1\Editor'
#
#  It compiles the pure-logic Runtime files plus a tiny UnityEngine stand-in,
#  runs the assertions in tools/RuleCheck/RuleCheck.cs, and exits 0 when all pass.
#
#  NOTE: this file is deliberately ASCII-only. Windows PowerShell 5.1 reads .ps1
#  files using the local ANSI code page unless they carry a UTF-8 BOM, so non-ASCII
#  text here would be mangled (and can even break parsing).
# =============================================================================
param(
    [string]$UnityEditor = 'E:\2022.3.62f3c1\Editor'
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is <project>\tools, so its parent is the project root.
$root = Split-Path -Parent $PSScriptRoot
$mb   = Join-Path $UnityEditor 'Data\MonoBleedingEdge'
$csc  = Join-Path $mb 'lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$mono = Join-Path $mb 'bin\mono.exe'

if (-not (Test-Path $csc))  { throw "Roslyn compiler not found: $csc" }
if (-not (Test-Path $mono)) { throw "mono not found: $mono" }

$outDir = Join-Path $env:TEMP 'fishing-rulecheck'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$exe = Join-Path $outDir 'RuleCheck.exe'

# Pure-logic files only: these never touch a UI type.
$runtime = Join-Path $root 'Assets\Scripts\Runtime'
$sources = @(
    (Join-Path $PSScriptRoot 'RuleCheck\UnityShim.cs'),
    (Join-Path $runtime 'Cfg.cs'),
    (Join-Path $runtime 'FishDefs.cs'),
    (Join-Path $runtime 'Patterns.cs'),
    (Join-Path $runtime 'Rng.cs'),
    (Join-Path $runtime 'PathMaker.cs'),
    (Join-Path $runtime 'GaugeKit.cs'),
    (Join-Path $runtime 'Loot.cs'),
    (Join-Path $runtime 'GameState.cs'),
    (Join-Path $runtime 'Sim.cs'),
    (Join-Path $PSScriptRoot 'RuleCheck\RuleCheck.cs')
)

foreach ($f in $sources) { if (-not (Test-Path $f)) { throw "Missing source: $f" } }

$rsp = Join-Path $outDir 'rulecheck.rsp'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-target:exe')
$lines.Add('-langversion:9.0')
$lines.Add('-out:"' + $exe + '"')
$lines.Add('-nowarn:0169,0414,0649,0219,0067')
foreach ($f in $sources) { $lines.Add('"' + $f + '"') }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

Write-Host "Compiling rule check..." -ForegroundColor Cyan
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
if ($code -eq 0) { Write-Host "RULE CHECK: ALL PASSED" -ForegroundColor Green }
else { Write-Host "RULE CHECK: FAILURES PRESENT" -ForegroundColor Red }
exit $code
