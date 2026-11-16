#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'Both')][string]$Configuration = 'Both',
    [switch]$Native,
    [switch]$IncludeRegression,
    [string]$EvidenceDirectory = 'artifacts/g1/validation'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath((Join-Path $repo $EvidenceDirectory))
if (!$evidence.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence must be inside this worktree.'
}
if (Test-Path -LiteralPath $evidence) { throw 'Use a new evidence directory to preserve earlier results.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$runs = [Collections.Generic.List[object]]::new()
function Invoke-Logged([string]$Name, [string]$Command, [string[]]$Arguments) {
    & $Command @Arguments *> (Join-Path $evidence "$Name.log")
    $code = $LASTEXITCODE
    $runs.Add([pscustomobject]@{ name = $Name; exitCode = $code; command = $Command; arguments = $Arguments })
    Write-Host "$Name : exit $code"
    return $code
}
$alreadyPrepared = $null -ne $global:CwbPrismEnvironment
Push-Location $repo
try {
    . ./eng/prism/Enter-Prism.ps1
    $configurations = if ($Configuration -eq 'Both') { @('Debug', 'Release') } else { @($Configuration) }
    foreach ($config in $configurations) {
        $project = 'prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj'
        $restore = Invoke-Logged "restore-$config" dotnet @('restore', $project, '--locked-mode', "-p:Configuration=$config")
        if ($restore -eq 0) {
            $test = Invoke-Logged "prototype-$config" dotnet @('test', $project, '-c', $config, '--no-restore', '--logger', "trx;LogFileName=prototype-$config.trx", '--results-directory', $evidence)
            if ($Native -and $test -eq 0) {
                $binary = "prototypes/G1DockFeasibility/bin/$config/net10.0/G1DockFeasibility"
                $nativeOutput = Join-Path $evidence "native-$config.json"
                if ($IsWindows) { $null = Invoke-Logged "native-$config" "./$binary.exe" @('--native-probe', $nativeOutput) }
                else { $null = Invoke-Logged "native-$config" dotnet @("$binary.dll", '--native-probe', $nativeOutput) }
            }
        }
        if ($IncludeRegression) {
            $build = Invoke-Logged "product-build-$config" pwsh @('-NoProfile', '-File', 'eng/prism/Build.ps1', '-Configuration', $config)
            if ($build -eq 0) {
                foreach ($suite in @('CryptoWorkBenchGuiTest', 'CryptoWorkBench.Mcp.Tests', 'CryptoScriptUnitTest')) {
                    $null = Invoke-Logged "$suite-$config" dotnet @('test', "$suite/$suite.csproj", '-c', $config, '--no-build', '--no-restore', '--logger', "trx;LogFileName=$suite-$config.trx", '--results-directory', $evidence)
                }
            }
        }
    }
} finally {
    $runs | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'commands.json')
    if (!$alreadyPrepared) { . ./eng/prism/Leave-Prism.ps1 }
    Pop-Location
}
if ($runs.Where({ $_.exitCode -ne 0 }).Count) { throw "Validation failed. See $evidence" }
