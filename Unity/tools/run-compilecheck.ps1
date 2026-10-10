# =============================================================================
#  Compile check for the raft <-> ocean buoyancy feature.
#
#  Compiles the feature's runtime files and the editor installer against the REAL
#  Unity / Crest / Assembly-CSharp assemblies using the Roslyn bundled with Unity,
#  in the same two-assembly shape Unity uses:
#      pass 1  Assets/Scripts/DivePoint/...  -> RaftRuntimeCheck.dll
#      pass 2  Assets/Editor/...             -> RaftEditorCheck.dll  (refs pass 1)
#
#  Why this exists: the project may be open in the editor, so dropping a file with
#  a typo into Assets/ triggers a broken domain reload. This catches that in a
#  second, headlessly.
#
#  The response file is written ASCII-only and csc runs with the project root as
#  the working directory, so non-ASCII project paths never hit encoding guesswork.
#
#  Usage:
#      powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-compilecheck.ps1
#
#  NOTE: ASCII-only, same reason as the other tools here.
# =============================================================================
param(
    [string]$UnityEditor = 'E:\2022.3.62f3c1\Editor',
    [switch]$StrictWarnings
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mb   = Join-Path $UnityEditor 'Data\MonoBleedingEdge'
$csc  = Join-Path $mb 'lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$mono = Join-Path $mb 'bin\mono.exe'

if (-not (Test-Path $csc))  { throw "Roslyn compiler not found: $csc" }
if (-not (Test-Path $mono)) { throw "mono not found: $mono" }

$outDir = Join-Path $env:TEMP 'whalefall-buoyancybuild'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$rtDll = Join-Path $outDir 'RaftRuntimeCheck.dll'
$edDll = Join-Path $outDir 'RaftEditorCheck.dll'

# ---- references, Unity style: netstandard 2.1 + the netfx/netstandard shims ----
$refs = New-Object System.Collections.Generic.List[string]

$netstandard = Join-Path $UnityEditor 'Data\NetStandard\ref\2.1.0\netstandard.dll'
if (-not (Test-Path $netstandard)) { throw "netstandard 2.1 ref not found: $netstandard" }
$refs.Add($netstandard)

$shimRoot = Join-Path $UnityEditor 'Data\NetStandard\compat\2.1.0\shims'
foreach ($dir in @('netfx', 'netstandard')) {
    $p = Join-Path $shimRoot $dir
    if (-not (Test-Path $p)) { throw "shim folder not found: $p" }
    foreach ($d in (Get-ChildItem $p -Filter *.dll)) { $refs.Add($d.FullName) }
}

$managed = Join-Path $UnityEditor 'Data\Managed'
$refs.Add((Join-Path $managed 'UnityEngine.dll'))
$refs.Add((Join-Path $managed 'UnityEditor.dll'))
foreach ($d in (Get-ChildItem (Join-Path $managed 'UnityEngine') -Filter *.dll)) { $refs.Add($d.FullName) }

# project assemblies, referenced relatively so the response file stays ASCII
foreach ($n in @('UnityEngine.UI.dll', 'Crest.dll')) {
    $rel = Join-Path 'Library\ScriptAssemblies' $n
    if (Test-Path (Join-Path $root $rel)) { $refs.Add($rel) }
    else { Write-Host "WARN missing reference: $rel" -ForegroundColor Yellow }
}

# ---- sources (relative to the project root) ----
# pass 1 rebuilds the project's runtime assembly the way Unity does: EVERY file under
# Assets/Scripts, so the check sees exactly the same source set Unity will.
#
# Two layouts are supported:
#   * in-place   : <root>/Assets/Scripts is the real project tree
#   * sandbox    : <root>/proj/Assets/Scripts is a copy of the project tree, and the
#                  files under <root>/Assets/Scripts/DivePoint are the modified ones
#                  that must replace their counterparts in the copy.
$staged = @(
    'Assets\Scripts\DivePoint\RaftBuoyancySolver.cs',
    'Assets\Scripts\DivePoint\RaftBuoyancy.cs',
    'Assets\Scripts\DivePoint\RaftDeckRider.cs',
    'Assets\Scripts\DivePoint\BuoyantObject.cs',
    'Assets\Scripts\DivePoint\RaftWeatherController.cs'
)

$sandbox = Join-Path $root 'proj\Assets\Scripts'
$inSandbox = Test-Path $sandbox
$walkRoot = if ($inSandbox) { $sandbox } else { Join-Path $root 'Assets\Scripts' }

$overlaid = @{}
if ($inSandbox) { foreach ($f in $staged) { $overlaid[(Split-Path $f -Leaf)] = $true } }

$runtimeSources = New-Object System.Collections.Generic.List[string]
foreach ($f in (Get-ChildItem -Recurse -File $walkRoot -Filter *.cs)) {
    if ($overlaid.ContainsKey($f.Name)) { continue }   # replaced by the staged version
    $runtimeSources.Add($f.FullName.Substring($root.Length + 1))
}
if ($inSandbox) { foreach ($f in $staged) { $runtimeSources.Add($f) } }

$editorSources = @(
    'Assets\Editor\RaftBuoyancySetup.cs',
    'Assets\Editor\RaftUiSkinSetup.cs',
    'Assets\Editor\RaftSkySetup.cs',
    'Assets\Editor\UnifiedOceanRaftBuilder.cs',
    'Assets\Editor\Raft3DPlayerSetup.cs'
)

foreach ($f in ($staged + $editorSources)) {
    if (-not (Test-Path (Join-Path $root $f))) { throw "Missing source: $f" }
}
Write-Host ("layout: " + $(if ($inSandbox) { 'sandbox' } else { 'in-place' }) + ", runtime sources: " + $runtimeSources.Count) -ForegroundColor DarkGray

# 0436: in sandbox mode a staged file can shadow its counterpart in the copied tree.
# 0618: Crest itself calls a few obsolete APIs.
# Everything else stays on so warnings from this feature actually surface.
$nowarn = if ($StrictWarnings) { '0436,0618' } else { '0169,0414,0649,0219,0067,0436,0618,0672,0108,0114' }
$common = @(
    '-target:library',
    '-langversion:9.0',
    '-nostdlib+',
    ('-nowarn:' + $nowarn)
)

function Show-Warnings([string]$tag, [string[]]$out) {
    $w = @($out | Where-Object { $_ -match ': warning ' })
    if ($w.Count -eq 0) { return 0 }
    # only surface warnings that touch this feature's own files
    $mine = @($w | Where-Object { $_ -match 'DivePoint\\Raft|DivePoint\\BuoyantObject|Editor\\RaftBuoyancySetup' })
    Write-Host ("  " + $tag + " warnings: " + $w.Count + " (this feature: " + $mine.Count + ")") -ForegroundColor Yellow
    $mine | Select-Object -First 25 | ForEach-Object { Write-Host ("    " + $_) -ForegroundColor Yellow }
    return $w.Count
}

function Invoke-Csc([string]$tag, [string[]]$sources, [string]$out, [string[]]$extraRefs) {
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($a in $common) { $lines.Add($a) }
    $lines.Add('-out:"' + $out + '"')
    foreach ($r in $refs) { $lines.Add('-reference:"' + $r + '"') }
    foreach ($r in $extraRefs) { $lines.Add('-reference:"' + $r + '"') }
    foreach ($s in $sources) { $lines.Add('"' + $s + '"') }

    $rsp = Join-Path $outDir ($tag + '.rsp')
    # ASCII only: every path above is ASCII either absolutely or relative to $root
    [System.IO.File]::WriteAllLines($rsp, $lines, (New-Object System.Text.ASCIIEncoding))

    $out2 = & $mono $csc ("@" + $rsp) 2>&1 | ForEach-Object { $_.ToString() }
    return @{ out = $out2; errs = @($out2 | Where-Object { $_ -match ': error ' }) }
}

Push-Location $root
try {
    Write-Host "=== pass 1/2 : runtime ($($runtimeSources.Count) files) ===" -ForegroundColor Cyan
    $r1 = Invoke-Csc 'runtime' $runtimeSources $rtDll @()
    if ($r1.errs.Count -gt 0) {
        Write-Host "RUNTIME COMPILE FAILED:" -ForegroundColor Red
        $r1.errs | Select-Object -First 40 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        exit 2
    }
    $w1 = Show-Warnings 'runtime' $r1.out
    Write-Host ("  OK -> RaftRuntimeCheck.dll") -ForegroundColor Green

    Write-Host "=== pass 2/2 : editor ($($editorSources.Count) files) ===" -ForegroundColor Cyan
    $r2 = Invoke-Csc 'editor' $editorSources $edDll @($rtDll)
    if ($r2.errs.Count -gt 0) {
        Write-Host "EDITOR COMPILE FAILED:" -ForegroundColor Red
        $r2.errs | Select-Object -First 40 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        exit 2
    }
    $w2 = Show-Warnings 'editor' $r2.out
    Write-Host ("  OK -> RaftEditorCheck.dll") -ForegroundColor Green
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "COMPILE CHECK: ALL PASSED" -ForegroundColor Green
exit 0
