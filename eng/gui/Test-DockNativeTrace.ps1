#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Trace,
    [Parameter(Mandatory)][long]$SourceHwnd,
    [Parameter(Mandatory)][long]$BeforeSequence,
    [Parameter(Mandatory)][long]$AfterSequence,
    [long[]]$RetainedHwnd = @(),
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Output) { throw 'Use a new output file; preserve earlier evidence.' }
$records = @(Get-Content -LiteralPath $Trace | ForEach-Object { $_ | ConvertFrom-Json -Depth 50 })
$before = @($records | Where-Object sequence -eq $BeforeSequence)
$after = @($records | Where-Object sequence -eq $AfterSequence)
if ($before.Count -ne 1 -or $after.Count -ne 1 -or $AfterSequence -le $BeforeSequence) { throw 'Select two existing ordered trace sequence numbers.' }
$before = $before[0]; $after = $after[0]
if ($before.pid -ne $after.pid) { throw 'Trace must describe one process.' }
$interval = @($records | Where-Object { $_.sequence -ge $BeforeSequence -and $_.sequence -le $AfterSequence })
$source = @($before.managedWindows | Where-Object lastHwnd -eq $SourceHwnd)
$checks = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [bool]$Passed) { $checks.Add([pscustomobject]@{name=$Name;passed=$Passed}) }
Check 'source-was-native-visible' (@($before.nativeWindows | Where-Object { $_.hwnd -eq $SourceHwnd -and $_.visible }).Count -eq 1)
Check 'source-was-registered-variables-host' ($source.Count -eq 1 -and $source[0].registered -and @($source[0].model.panes | Where-Object Id -eq 'tool:variables').Count -eq 1)
Check 'mouse-press-observed' (@($interval | Where-Object reason -like 'MouseDown:*').Count -gt 0)
Check 'mouse-release-completed' (@($interval | Where-Object reason -eq 'MouseUp:dispatch-complete').Count -gt 0)
Check 'dock-mutation-observed' (@($interval | Where-Object reason -match '^(MoveDockable|SplitToDock):after$').Count -gt 0)
Check 'source-native-hwnd-destroyed' (@($after.nativeWindows | Where-Object hwnd -eq $SourceHwnd).Count -eq 0)
$expectedVisible = [Collections.Generic.HashSet[long]]::new()
foreach ($native in $before.nativeWindows) {
    if ($native.visible -and $native.hwnd -ne $SourceHwnd) { [void]$expectedVisible.Add($native.hwnd) }
}
$actualVisible = [long[]]@($after.nativeWindows | Where-Object visible | ForEach-Object hwnd)
Check 'complete-visible-native-window-set' ($expectedVisible.SetEquals($actualVisible))
Check 'source-managed-host-closed' (@($after.managedWindows | Where-Object { $_.lastHwnd -eq $SourceHwnd -and $_.closed -eq $true -and !$_.nativeAlive -and !$_.registered }).Count -eq 1)
# An unregistered live HWND is a failure even if the factory's model list is empty.
$orphans = @($after.nativeWindows | Where-Object {
    $_.visible -and $_.managedType -like '*HostWindow*' -and !$_.factoryRegistered
})
Check 'no-visible-unregistered-hosts' ($orphans.Count -eq 0)
$overlays = @($after.nativeWindows | Where-Object {
    $_.visible -and ($_.managedType -match 'DragPreview|DockAdorner' -or ($_.managedIdentity -eq 0 -and $_.wsExAppWindow))
})
Check 'no-visible-drag-or-unmapped-app-windows' ($overlays.Count -eq 0)
foreach ($hwnd in $RetainedHwnd) {
    Check "legitimate-window-retained-$hwnd" (@($after.nativeWindows | Where-Object { $_.hwnd -eq $hwnd -and $_.visible -and $_.factoryRegistered }).Count -eq 1)
}
$failed = @($checks | Where-Object { !$_.passed }).Count
[ordered]@{
    acceptance = 'Automated evaluation only; M2 requires separate owner acceptance. M3-M8 remain OPEN.'
    input = 'Recorded real desktop mouse interaction; no Factory calls performed by this evaluator'
    trace = (Resolve-Path -LiteralPath $Trace).Path; traceSha256 = (Get-FileHash -LiteralPath $Trace).Hash
    pid = $after.pid; sourceHwnd = $SourceHwnd; beforeSequence = $BeforeSequence; afterSequence = $AfterSequence
    passed = $checks.Count - $failed; failed = $failed; checks = $checks; orphanWindows = $orphans; overlayWindows = $overlays
} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Output
if ($failed) { throw "$failed native regression checks failed; see $Output" }
