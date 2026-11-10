#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release','Both')][string]$Configuration = 'Both',
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$IncludeHistorical
)
$ErrorActionPreference='Stop'
if (!$IsWindows) { throw 'This acceptance driver requires native Windows.' }
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $repo $EvidenceDirectory))
if (!$evidence.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must remain in this worktree.' }
if (Test-Path $evidence) { throw 'Use a fresh evidence directory.' }
New-Item -ItemType Directory $evidence | Out-Null
$runs=[Collections.Generic.List[object]]::new()
$prepared=$null -ne $global:CwbPrismEnvironment
Push-Location $repo
try {
    . ./eng/prism/Enter-Prism.ps1
    if ($IncludeHistorical) {
        & ./eng/gui/Validate-G1LayoutRoles.ps1 -Configuration $Configuration -EvidenceDirectory (Join-Path $EvidenceDirectory 'historical') -IncludeExistingNative -PersistenceCleanup -OperationContract -PartialFailure
    }
    $configs=if($Configuration -eq 'Both'){@('Debug','Release')}else{@($Configuration)}
    foreach($config in $configs) {
        & dotnet restore eng/gui/native/G1ShellProbe.csproj --locked-mode "-p:Configuration=$config" -m:1 *> "$evidence/restore-$config.log"
        $runs.Add(@{name="restore-$config";exitCode=$LASTEXITCODE})
        if($LASTEXITCODE -ne 0){throw 'Native restore failed'}
        & dotnet build eng/gui/native/G1ShellProbe.csproj -c $config --no-restore -m:1 *> "$evidence/build-$config.log"
        $runs.Add(@{name="build-$config";exitCode=$LASTEXITCODE})
        if($LASTEXITCODE -ne 0){throw 'Native build failed'}
        foreach($mode in @('native-acceptance','native-acceptance','tools','output','diagnostics')) {
            $name="$config-$mode-$($runs.Count)"
            $output=Join-Path $evidence "$name.json"
            $process=Start-Process -FilePath (Join-Path $repo "eng/gui/native/bin/$config/net10.0/G1ShellProbe.exe") -ArgumentList @($mode,('"'+$output+'"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output.out" -RedirectStandardError "$output.err"
            $finished=$process.WaitForExit(180000)
            if(!$finished){Stop-Process -Id $process.Id -Force}
            $process.Refresh()
            $runs.Add(@{name=$name;mode=$mode;processId=$process.Id;exitCode=$process.ExitCode;timedOut=!$finished;output=$output})
            $runs | ConvertTo-Json -Depth 4 | Set-Content "$evidence/commands.json"
            Write-Host "$name exit $($process.ExitCode), timeout=$(!$finished)"
        }
    }
} finally {
    $runs | ConvertTo-Json -Depth 4 | Set-Content "$evidence/commands.json"
    if(!$prepared){. ./eng/prism/Leave-Prism.ps1}
    Pop-Location
}
if($runs.Where({$_.exitCode -ne 0 -or $_.timedOut}).Count){throw 'Native acceptance failure; inspect retained evidence.'}
