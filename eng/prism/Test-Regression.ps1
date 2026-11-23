#requires -Version 7.4
[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$results=Join-Path $repo "artifacts/prism-p1/$Configuration"
New-Item -ItemType Directory -Force -Path $results|Out-Null
Push-Location $repo
try{
    foreach($solution in 'CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln','CryptoWorkBench.sln'){
        $name=[IO.Path]::GetFileNameWithoutExtension($solution)
        & dotnet restore $solution --locked-mode "-p:Configuration=$Configuration" *> (Join-Path $results "$name-restore.log")
        if($LASTEXITCODE){throw "$name restore failed"}
        & dotnet build $solution -c $Configuration --no-restore *> (Join-Path $results "$name-build.log")
        if($LASTEXITCODE){throw "$name build failed"}
    }
    foreach($suite in 'Core','Gui','Mcp'){
        $project=switch($suite){'Core'{'CryptoScriptUnitTest/CryptoScriptUnitTest.csproj'}'Gui'{'CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj'}'Mcp'{'CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj'}}
        & dotnet test $project -c $Configuration --no-build --no-restore --logger "trx;LogFileName=$suite.trx" --results-directory $results --blame-hang --blame-hang-timeout 2m *> (Join-Path $results "$suite-test.log")
        if($LASTEXITCODE){throw "$suite tests failed"}
    }
    Write-Host "$Configuration full regressions passed; $results"
}finally{Pop-Location}
