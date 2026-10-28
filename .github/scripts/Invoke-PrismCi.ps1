#requires -Version 7.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidateSet('Build','Suite','Native','Publish')][string]$Stage,
 [ValidateSet('Debug','Release')][string]$Configuration='Release',
 [ValidateSet('Core','Gui','Coordinator','F1','C1','Mcp')][string]$Suite='Core',
 [ValidateSet('win-x64','linux-x64','osx-x64','osx-arm64')][string]$RuntimeIdentifier='win-x64'
)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$logRoot=Join-Path $repo 'artifacts/ci-logs'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$logName="$Stage-$Configuration-$Suite-$RuntimeIdentifier.log"
Start-Transcript -LiteralPath (Join-Path $logRoot $logName) -Force | Out-Null
Push-Location $repo
try {
 $sdk=& dotnet --version
 if($LASTEXITCODE -ne 0 -or $sdk.Trim() -ne '10.0.401'){throw "Required SDK 10.0.401; actual $sdk"}
 $ownsPrismEnvironment=$null -eq $global:CwbPrismEnvironment
 $previousHooks=@{}
 foreach($name in @('CustomBeforeDirectoryBuildProps','CustomAfterDirectoryBuildProps')){
  $previousHooks[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
 }
 . "$repo/eng/prism/Enter-Prism.ps1"
 try {
  switch($Stage) {
   'Build' { & "$repo/eng/prism/Build.ps1" -Configuration $Configuration }
   'Native' { & "$repo/.github/scripts/Test-F01Native.ps1" -RequireMacArm64:($RuntimeIdentifier -eq 'osx-arm64') }
   'Suite' {
    $project=switch($Suite){'Core'{'CryptoScriptUnitTest/CryptoScriptUnitTest.csproj'} 'C1'{'CryptoScriptUnitTest/CryptoScriptUnitTest.csproj'} 'Mcp'{'CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj'} default{'CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj'}}
    $filter=switch($Suite){'Coordinator'{'FullyQualifiedName~EditorCoordinatorTests'} 'F1'{'FullyQualifiedName~ScriptBookImportCancellationTests'} 'C1'{'FullyQualifiedName~RejectsInvalidPsnWithoutNormalizing'} default{''}}
    $args=@('test',$project,'-c',$Configuration,'--no-build','--no-restore','--logger',"trx;LogFileName=$Suite.trx",'--results-directory','artifacts/test-results','--blame-hang','--blame-hang-timeout','2m','--blame-hang-dump-type','mini')
    if($filter){$args+=@('--filter',$filter)}
    $resultPath=Join-Path $repo "artifacts/test-results/$Suite.trx"
    if(Test-Path -LiteralPath $resultPath){Remove-Item -LiteralPath $resultPath}
    & dotnet @args
    if($LASTEXITCODE){throw "$Suite failed: $LASTEXITCODE"}
    [xml]$trx=Get-Content "artifacts/test-results/$Suite.trx" -Raw
    if(@($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'Passed').Count -eq 0){throw "No passing tests discovered for $Suite"}
   }
   'Publish' {
    if($Configuration -ne 'Release'){throw 'RID publish locks support Release only.'}
    $project='CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj'
    & dotnet restore $project --locked-mode -p:Configuration=Release --runtime $RuntimeIdentifier "-p:CwbPublishRid=$RuntimeIdentifier" --configfile NuGet.Config
    if($LASTEXITCODE){throw "RID restore failed: $LASTEXITCODE"}
    # Transitive managed ProjectReferences may build without RuntimeIdentifier.
    # Do not allow a successful cross-publish to omit the required native shim.
    if($RuntimeIdentifier.StartsWith('osx-') -and -not $IsMacOS){
     throw 'macOS publish requires an actual macOS host with Xcode command line tools; a Windows/Linux cross-publish cannot build the required FileSystem shim.'
    }
    & dotnet publish $project --configuration Release --runtime $RuntimeIdentifier --self-contained false -p:UseAppHost=true "-p:CwbPublishRid=$RuntimeIdentifier" --no-restore --output "artifacts/publish/$RuntimeIdentifier"
    if($LASTEXITCODE){throw "RID publish failed: $LASTEXITCODE"}
   }
  }
 } finally {
  if($ownsPrismEnvironment){
   . "$repo/eng/prism/Leave-Prism.ps1"
   # PowerShell can bind $null as an empty string; preserve absence exactly.
   foreach($name in $previousHooks.Keys){
    if($null -eq $previousHooks[$name] -and [string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name,'Process'))){
     [Environment]::SetEnvironmentVariable($name,[NullString]::Value,'Process')
    }
   }
  }
 }
} finally { Pop-Location; Stop-Transcript | Out-Null }
