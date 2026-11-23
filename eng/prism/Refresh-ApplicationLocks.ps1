#requires -Version 7.4
# Explicit dependency maintenance. Never called by IDE, bootstrap or CI.
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repo
try {
    foreach($configuration in 'Debug','Release'){
        foreach($solution in 'CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln','CryptoWorkBench.sln'){
            & dotnet restore $solution "-p:Configuration=$configuration" -p:RestoreLockedMode=false --force-evaluate
            if($LASTEXITCODE){throw "Lock refresh failed: $configuration $solution"}
        }
    }
    foreach($rid in 'win-x64','linux-x64','osx-x64','osx-arm64'){
        & dotnet restore CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj -p:Configuration=Release --runtime $rid "-p:CwbPublishRid=$rid" -p:RestoreLockedMode=false --force-evaluate
        if($LASTEXITCODE){throw "RID lock refresh failed: $rid"}
    }
    foreach($project in 'eng/gui/native/G1ShellProbe.csproj','prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj'){
        & dotnet restore $project -p:Configuration=Debug -p:RestoreLockedMode=false --force-evaluate
        if($LASTEXITCODE){throw "GUI validation lock refresh failed: $project"}
    }
}finally{Pop-Location}
