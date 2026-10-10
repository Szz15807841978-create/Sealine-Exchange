param([string]$UnityEditor='E:\2022.3.62f3c1\Editor')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskMono=Join-Path $UnityEditor 'Data\MonoBleedingEdge\bin\mono.exe'
$taskCsc=Join-Path $UnityEditor 'Data\MonoBleedingEdge\lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$taskBuild=Join-Path $taskRoot 'tools\PlayerBuild'
New-Item -ItemType Directory -Force -Path $taskBuild | Out-Null
Push-Location $taskRoot
try {
    & powershell -NoProfile -ExecutionPolicy Bypass -File tools/run-compilecheck.ps1 -UnityEditor $UnityEditor
    if($LASTEXITCODE -ne 0){throw 'Runtime compile failed'}
    $taskRsp=Join-Path $env:TEMP 'whalefall-buoyancybuild\editor.rsp'
    $taskLines=@(Get-Content -LiteralPath $taskRsp | Where-Object {$_ -notmatch '^"Assets\\Editor\\' -and $_ -notmatch '^-out:'})
    $taskLines+='-out:"tools\PlayerBuild\PlayerEditorCheck.dll"'
    $taskLines+='"Assets\Editor\PlayerMarketSceneBuilder.cs"'
    if(Test-Path 'Assets\Editor\PlayerMarketPlayCheck.cs'){$taskLines+='"Assets\Editor\PlayerMarketPlayCheck.cs"'}
    if(Test-Path 'Assets\Editor\PlayerRaftIntegrationSetup.cs'){$taskLines+='"Assets\Editor\PlayerRaftIntegrationSetup.cs"'}
    if(Test-Path 'Assets\Editor\PlayerRaftIntegrationPlayCheck.cs'){$taskLines+='"Assets\Editor\PlayerRaftIntegrationPlayCheck.cs"'}
    if(Test-Path 'Assets\Editor\PlayerPaperSkinSetup.cs'){$taskLines+='"Assets\Editor\PlayerPaperSkinSetup.cs"'}
    if(Test-Path 'Assets\Editor\PlayerUiRepairSetup.cs'){$taskLines+='"Assets\Editor\PlayerUiRepairSetup.cs"'}
    if(Test-Path 'Assets\Editor\PlayerEditorRefresh.cs'){$taskLines+='"Assets\Editor\PlayerEditorRefresh.cs"'}
    $taskEditorRsp=Join-Path $taskBuild 'editor.rsp'
    [System.IO.File]::WriteAllLines($taskEditorRsp,$taskLines,[System.Text.Encoding]::UTF8)
    & $taskMono $taskCsc ('@'+$taskEditorRsp)
    if($LASTEXITCODE -ne 0){throw 'Player editor compile failed'}
    & $taskMono $taskCsc '-nologo' '-langversion:9' '-target:exe' '-out:tools/PlayerBuild/PlayerModelCheck.exe' 'Assets/Scripts/PlayerFishing/PlayerMarketModel.cs' 'tools/RuleCheck/PlayerMarketCheck.cs'
    if($LASTEXITCODE -ne 0){throw 'Player model test compile failed'}
    $taskNode=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    & $taskNode tools/compare-player-model.js $taskMono (Join-Path $taskBuild 'PlayerModelCheck.exe')
    if($LASTEXITCODE -ne 0){throw 'Player model parity failed'}
} finally {Pop-Location}
