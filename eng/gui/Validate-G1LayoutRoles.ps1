#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release','Both')][string]$Configuration = 'Both',
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$IncludeExistingNative,
    [switch]$PersistenceCleanup,
    [switch]$OperationContract,
    [switch]$OperationContractOnly,
    [switch]$PartialFailure
)
$ErrorActionPreference = 'Stop'
if (!$IsWindows) { throw 'The layout-role probe requires native Windows.' }
if ($OperationContractOnly -and ($IncludeExistingNative -or $PersistenceCleanup)) { throw 'OperationContractOnly cannot include the historical matrix.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath((Join-Path $repo $EvidenceDirectory))
if (!$evidence.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must remain in this worktree.' }
if (Test-Path -LiteralPath $evidence) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$runs = [Collections.Generic.List[object]]::new()
$prepared = $null -ne $global:CwbPrismEnvironment
Push-Location $repo
try {
    . ./eng/prism/Enter-Prism.ps1
    $configs = if ($Configuration -eq 'Both') { @('Debug','Release') } else { @($Configuration) }
    foreach ($config in $configs) {
        $project = 'eng/gui/native/G1ShellProbe.csproj'
        & dotnet restore $project --locked-mode "-p:Configuration=$config" -m:1 *> (Join-Path $evidence "restore-$config.log")
        $runs.Add([pscustomobject]@{ name = "restore-$config"; exitCode = $LASTEXITCODE })
        if ($LASTEXITCODE -ne 0) { throw 'Native restore failed.' }
        & dotnet build $project -c $config --no-restore -m:1 *> (Join-Path $evidence "build-$config.log")
        $runs.Add([pscustomobject]@{ name = "build-$config"; exitCode = $LASTEXITCODE })
        if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
        [string[]]$modes = if ($OperationContractOnly) { @() } else { @('layout-roles') }
        if ($IncludeExistingNative) { $modes += @('layout','layout-restart','layout-invalid','redock','editor','integration') }
        $cases = [Collections.Generic.List[object]]::new()
        if ($PartialFailure) {
            foreach ($repeat in 1..2) { $cases.Add(@{ mode = 'partial-failure'; suffix = "partial-failure-$repeat" }) }
        }
        foreach ($mode in $modes) { $cases.Add(@{ mode = $mode; suffix = $mode }) }
        if ($OperationContract -or $OperationContractOnly) {
            foreach ($repeat in 1..2) { $cases.Add(@{ mode = 'operation-contract'; suffix = "operation-contract-$repeat" }) }
        }
        if ($PersistenceCleanup) {
            foreach ($scenario in @('single','multiple','anchor','document','document-anchor','nested')) {
                $cases.Add(@{ mode = 'cleanup-save'; suffix = "cleanup-$scenario-save"; scenario = $scenario })
                foreach ($variant in @('current','legacy')) {
                    $cases.Add(@{ mode = 'cleanup-restore'; suffix = "cleanup-$scenario-$variant"; scenario = $scenario; variant = $variant; source = "$config-cleanup-$scenario-save.json.layout.json" })
                }
            }
            # Same genuine pre-A02 input is independently started twice.
            foreach ($repeat in 1..2) { $cases.Add(@{ mode = 'cleanup-restore'; suffix = "cleanup-historical-$repeat"; scenario = 'historical' }) }
        }
        foreach ($case in $cases) {
            $mode = $case.mode
            $name = "$config-$($case.suffix)"
            $output = Join-Path $evidence "$name.json"
            if ($mode -eq 'layout-restart') { Copy-Item -LiteralPath (Join-Path $evidence "$config-layout.json.layout.json") -Destination "$output.layout.json" }
            if ($mode -eq 'layout-invalid') { '{invalid' | Set-Content -LiteralPath "$output.layout.json" }
            if ($case.scenario) {
                $case.scenario | Set-Content -LiteralPath "$output.scenario.txt"
                if ($mode -eq 'cleanup-restore') {
                    $source = if ($case.scenario -eq 'historical') { Join-Path $repo 'CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json' } else { Join-Path $evidence $case.source }
                    if ($case.variant -eq 'legacy') {
                        [IO.File]::WriteAllText("$output.layout.json", [IO.File]::ReadAllText($source).Replace('dock:container:', 'legacy:container:'))
                    } else { Copy-Item -LiteralPath $source -Destination "$output.layout.json" }
                    Copy-Item -LiteralPath "$output.layout.json" -Destination "$output.input-layout.json"
                }
            }
            $process = Start-Process -FilePath (Join-Path $repo "eng/gui/native/bin/$config/net10.0/G1ShellProbe.exe") -ArgumentList @($mode, ('"' + $output + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidence "$name.out") -RedirectStandardError (Join-Path $evidence "$name.err")
            # Process bound, not a retry: a blocked probe is a failure with retained logs.
            $finished = $process.WaitForExit(180000)
            if (!$finished) { Stop-Process -Id $process.Id -Force }
            $process.Refresh()
            $runs.Add([pscustomobject]@{ name = $name; processId = $process.Id; exitCode = $process.ExitCode; timedOut = !$finished; output = $output })
            $runs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'commands.json')
            Write-Host "$name : exit $($process.ExitCode), timedOut=$(!$finished)"
        }
    }
} finally {
    $runs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'commands.json')
    if (!$prepared) { . ./eng/prism/Leave-Prism.ps1 }
    Pop-Location
}
if ($runs.Where({ $_.exitCode -ne 0 -or $_.timedOut }).Count) { throw "A native run failed. Inspect $evidence" }
