#requires -Version 7.0
# Dot-source before starting a NEW CLI/IDE process. Leave-Prism.ps1 restores the hooks.
& {
    $ErrorActionPreference = 'Stop'
    $hooks = @{
        CustomBeforeDirectoryBuildProps = Join-Path $PSScriptRoot 'Lock.props'
        CustomAfterDirectoryBuildProps = Join-Path $PSScriptRoot 'PrismSecurity.targets'
    }
    # Validate everything before changing the environment. Do not replace somebody else's hooks.
    foreach ($name in $hooks.Keys) {
        $current = [Environment]::GetEnvironmentVariable($name, 'Process')
        if ($current -and $current -ne $hooks[$name]) {
            throw "Existing $name is preserved. Use a separate clean PowerShell process for the temporary Prism build."
        }
    }
    if ($env:CustomAfterMicrosoftCommonProps -eq (Join-Path $PSScriptRoot 'Lock.props') -or $env:CustomAfterMicrosoftCommonTargets -eq (Join-Path $PSScriptRoot 'PrismSecurity.targets')) {
        throw 'Legacy Prism environment detected. Start a new PowerShell process before using the corrected integration.'
    }
    & (Join-Path $PSScriptRoot 'Prepare-Prism.ps1')
    if (!$global:CwbPrismEnvironment) {
        $previous = @{}
        foreach ($name in $hooks.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
        $global:CwbPrismEnvironment = @{ Root = $PSScriptRoot; Previous = $previous; Applied = $hooks }
    }
    foreach ($name in $hooks.Keys) { [Environment]::SetEnvironmentVariable($name, $hooks[$name], 'Process') }
    # NuGet cache normalization belongs to our scoped MSBuild properties, not the caller's environment.
}

