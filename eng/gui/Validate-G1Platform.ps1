#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release','Both')][string]$Configuration = 'Both',
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$Desktop
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath((Join-Path $repo $EvidenceDirectory))
if (!$evidence.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) { throw 'Evidence must be inside this checkout.' }
if (Test-Path -LiteralPath $evidence) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$result = [ordered]@{ platform = [Runtime.InteropServices.RuntimeInformation]::OSDescription; architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString(); configuration = $Configuration; buildAndTests = 'not run'; nativeAcceptance = 'OPEN: requires recorded manual checklist'; desktopExit = $null }
Push-Location $repo
try {
    & ./eng/gui/Validate-G1Shell.ps1 -Configuration $Configuration -EvidenceDirectory "$EvidenceDirectory/product"
    & ./prototypes/G1DockFeasibility/Validate.ps1 -Configuration $Configuration -EvidenceDirectory "$EvidenceDirectory/prototype"
    $result.buildAndTests = 'passed; inspect all commands.json and TRX counters'
    if ($Desktop) {
        if ($IsLinux -and !$env:DISPLAY -and !$env:WAYLAND_DISPLAY) { throw 'A real Linux desktop session is required; headless success is not desktop acceptance.' }
        $config = if ($Configuration -eq 'Both') { 'Release' } else { $Configuration }
        Write-Host 'Complete docs/gui/G1-08-native-acceptance-checklist.md. Closing the app does not certify acceptance.'
        & dotnet "CryptoWorkBenchAvalonia.Desktop/bin/$config/net10.0/CryptoWorkBenchAvalonia.Desktop.dll" --dock-shell *> (Join-Path $evidence 'desktop.log')
        $result.desktopExit = $LASTEXITCODE
        if ($LASTEXITCODE -ne 0) { throw 'Desktop process failed; preserve desktop.log.' }
    }
} finally {
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'platform.json')
    Pop-Location
}
