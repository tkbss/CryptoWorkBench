#requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$alreadyPrepared = $null -ne $global:CwbPrismEnvironment
. (Join-Path $PSScriptRoot 'Enter-Prism.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repo
try {
    foreach ($solution in @('CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln', 'CryptoWorkBench.sln')) {
        & dotnet restore $solution --locked-mode "-p:Configuration=$Configuration" --configfile (Join-Path $repo 'NuGet.Config')
        if ($LASTEXITCODE -ne 0) { throw "Restore failed: $solution" }
        & dotnet build $solution -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $solution" }
    }
} finally {
    Pop-Location
    if (!$alreadyPrepared) { . (Join-Path $PSScriptRoot 'Leave-Prism.ps1') }
}
