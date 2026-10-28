#requires -Version 7.0
# Dot-source in the same shell after the CLI/IDE session has ended.
& {
    if ($global:CwbPrismEnvironment -and $global:CwbPrismEnvironment.Root -eq $PSScriptRoot) {
        foreach ($name in $global:CwbPrismEnvironment.Applied.Keys) {
            # Preserve changes deliberately made by the caller after Enter-Prism.
            if ([Environment]::GetEnvironmentVariable($name, 'Process') -eq $global:CwbPrismEnvironment.Applied[$name]) {
                [Environment]::SetEnvironmentVariable($name, $global:CwbPrismEnvironment.Previous[$name], 'Process')
            }
        }
        Remove-Variable CwbPrismEnvironment -Scope Global
    }
}
