#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'Both')][string]$Configuration = 'Both',
    [switch]$Native,
    [switch]$EditorIntegration,
    [switch]$ToolIntegration,
    [switch]$OutputIntegration,
    [switch]$DiagnosticsIntegration,
    [switch]$LayoutIntegration,
    [string]$EvidenceDirectory = 'artifacts/g1-02/validation'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath((Join-Path $repo $EvidenceDirectory))
if (!$evidence.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence must be inside this worktree.'
}
if (Test-Path -LiteralPath $evidence) { throw 'Use a new evidence directory to preserve earlier results.' }
if ($Native -and !$IsWindows) { throw 'The native G1-02 probe currently supports Windows only.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$runs = [Collections.Generic.List[object]]::new()
function Invoke-Logged([string]$Name, [string]$Command, [string[]]$Arguments) {
    & $Command @Arguments *> (Join-Path $evidence "$Name.log")
    $code = $LASTEXITCODE
    $runs.Add([pscustomobject]@{ name = $Name; exitCode = $code; command = $Command; arguments = $Arguments })
    Write-Host "$Name : exit $code"
    if ($code -ne 0) { throw "$Name failed. See $evidence" }
}
$alreadyPrepared = $null -ne $global:CwbPrismEnvironment
Push-Location $repo
try {
    . ./eng/prism/Enter-Prism.ps1
    $configurations = if ($Configuration -eq 'Both') { @('Debug', 'Release') } else { @($Configuration) }
    foreach ($config in $configurations) {
        Invoke-Logged "build-$config" pwsh @('-NoProfile', '-File', 'eng/prism/Build.ps1', '-Configuration', $config)
        foreach ($suite in @('CryptoWorkBenchGuiTest', 'CryptoWorkBench.Mcp.Tests', 'CryptoScriptUnitTest')) {
            Invoke-Logged "$suite-$config" dotnet @('test', "$suite/$suite.csproj", '-c', $config, '--no-build', '--no-restore', '--logger', "trx;LogFileName=$suite-$config.trx", '--results-directory', $evidence)
        }
        if ($Native) {
            $project = 'eng/gui/native/G1ShellProbe.csproj'
            Invoke-Logged "native-restore-$config" dotnet @('restore', $project, '--locked-mode', "-p:Configuration=$config")
            Invoke-Logged "native-build-$config" dotnet @('build', $project, '-c', $config, '--no-restore')
            $modes = @('dock', 'legacy', 'fallback')
            if ($EditorIntegration) { $modes += @('editor', 'startup-failure') }
            if ($ToolIntegration) { $modes += 'tools' }
            if ($OutputIntegration) { $modes += 'output' }
            if ($DiagnosticsIntegration) { $modes += 'diagnostics' }
            if ($LayoutIntegration) { $modes += @('layout', 'layout-restart', 'layout-invalid') }
            foreach ($mode in $modes) {
                $name = "native-$config-$mode"
                $output = Join-Path $evidence "$name.json"
                if ($mode -eq 'layout-restart') {
                    Copy-Item -LiteralPath (Join-Path $evidence "native-$config-layout.json.layout.json") -Destination "$output.layout.json"
                }
                if ($mode -eq 'layout-invalid') { '{invalid layout' | Set-Content -LiteralPath "$output.layout.json" }
                $process = Start-Process -FilePath (Join-Path $repo "eng/gui/native/bin/$config/net10.0/G1ShellProbe.exe") -ArgumentList @($mode, ('"' + $output + '"')) -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $evidence "$name.out") -RedirectStandardError (Join-Path $evidence "$name.err")
                $runs.Add([pscustomobject]@{ name = $name; exitCode = $process.ExitCode; output = $output })
                Write-Host "$name : exit $($process.ExitCode)"
                if ($process.ExitCode -ne 0) { throw "$name failed. See $output" }
            }
        }
    }
} finally {
    $runs | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'commands.json')
    if (!$alreadyPrepared) { . ./eng/prism/Leave-Prism.ps1 }
    Pop-Location
}
