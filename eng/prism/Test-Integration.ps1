#requires -Version 7.0
# Focused integration regressions. All mutations are confined to a new evidence fixture.
[CmdletBinding()]
param([string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
if ($global:CwbPrismEnvironment) { throw 'Run these fixture tests in a new pwsh process.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$EvidenceDirectory) { throw 'Provide a NEW evidence directory inside this repository.' }
$out = [IO.Path]::GetFullPath($EvidenceDirectory, $repo)
if (!$out.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path $out)) { throw 'Evidence directory must be new and inside this repository.' }
New-Item -ItemType Directory -Path $out | Out-Null
$fixture = Join-Path $out 'fixture'
New-Item -ItemType Directory -Force "$fixture/eng/prism", "$fixture/CryptoWorkBenchAvalonia", "$out/foreign" | Out-Null
Copy-Item "$PSScriptRoot/*" "$fixture/eng/prism" -Recurse
Copy-Item "$repo/NuGet.Config", "$repo/global.json", "$repo/Directory.Build.targets" $fixture
# Minimal project at the exact approved identity exercises policy without product edits.
'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>' | Set-Content "$fixture/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj"
foreach ($cfg in @('Debug','Release')) {
    '{"version":1,"dependencies":{"net10.0":{}}}' | Set-Content "$fixture/eng/prism/locks/$cfg/CryptoWorkBenchAvalonia.lock.json"
}
$results = [Collections.Generic.List[object]]::new()
function Check([string]$name, [bool]$passed, $detail) {
    $results.Add([pscustomobject]@{name=$name;passed=$passed;detail=$detail})
    $results | ConvertTo-Json -Depth 8 | Set-Content "$out/summary.json"
    if (!$passed) { throw "Regression failed: $name ($detail)" }
}
function Run([string]$name, [string[]]$arguments, [bool]$success = $true) {
    $arguments | ConvertTo-Json | Set-Content "$out/$name.command.json"
    & dotnet @arguments *> "$out/$name.log"
    $code = $LASTEXITCODE
    Check $name (($code -eq 0) -eq $success) "exit=$code"
}
function Prepare([string]$name, [bool]$success) {
    & pwsh -NoProfile -File "$fixture/eng/prism/Prepare-Prism.ps1" *> "$out/$name.log"
    Check $name (($LASTEXITCODE -eq 0) -eq $success) "exit=$LASTEXITCODE"
}
$envNames = @('CustomBeforeDirectoryBuildProps','CustomAfterMicrosoftCommonProps','CustomAfterDirectoryBuildProps','DISABLE_GITVERSIONING','GeneratePackageOnBuild','NuGetAudit','NuGetAuditMode','NUGET_PACKAGES')
$saved = @{}
foreach ($name in $envNames) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    Prepare 'prepare-fresh' $true
    $checkout = "$fixture/.deps/prism"
    $app = "$fixture/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj"
    $prism = "$checkout/src/Prism.Events/Prism.Events.csproj"
    $foreign = "$out/foreign/OtherApplication.csproj"
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><GeneratePackageOnBuild>true</GeneratePackageOnBuild><DISABLE_GITVERSIONING>false</DISABLE_GITVERSIONING><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>' | Set-Content $foreign
    Copy-Item $foreign "$out/foreign/Prism.Events.csproj"
    $foreignArgs = @('-p:ImportDirectoryBuildProps=false','-p:ImportDirectoryBuildTargets=false')
    $propertyQuery = '-getProperty:NuGetLockFilePath,RestoreLockedMode,GeneratePackageOnBuild,DISABLE_GITVERSIONING,NuGetAudit,NuGetAuditMode'
    Run 'foreign-before' (@('msbuild',$foreign,$propertyQuery) + $foreignArgs)
    . "$fixture/eng/prism/Enter-Prism.ps1"
    . "$fixture/eng/prism/Enter-Prism.ps1"
    Run 'default-restore' @('restore',$app,'--locked-mode','--force')
    foreach ($cfg in @('Default','Debug','Release')) {
        $configArgs = if ($cfg -eq 'Default') { @() } else { @("-p:Configuration=$cfg") }
        foreach ($pair in @(@('app',$app),@('outer',$prism),@('inner',$prism))) {
            $name = "lock-$cfg-$($pair[0])"
            $args = @('msbuild',$pair[1],'-getProperty:Configuration,NuGetLockFilePath,GeneratePackageOnBuild') + $configArgs
            if ($pair[0] -eq 'inner') { $args += '-p:TargetFramework=net10.0' }
            Run $name $args
            $properties = (Get-Content "$out/$name.log" -Raw | ConvertFrom-Json).Properties
            $expected = if ($cfg -eq 'Default') { 'Debug' } else { $cfg }
            Check "$name-no-auto-pack" ($properties.GeneratePackageOnBuild -eq 'false') $properties.GeneratePackageOnBuild
            Check "$name-path" ($properties.Configuration -eq $expected -and $properties.NuGetLockFilePath.Replace('\','/').Contains("/locks/$expected/")) $properties
        }
    }
    $lock = "$fixture/eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json"
    $valid = Get-Content $lock -Raw
    Move-Item -LiteralPath $lock -Destination "$out/saved-debug-lock.json"
    Run 'missing-default-lock' @('restore',$app,'--locked-mode','--force') $false
    Check 'missing-lock-explained' ((Get-Content "$out/missing-default-lock.log" -Raw).Contains('Required temporary Prism lockfile is missing')) $null
    'deliberately invalid F2 lock' | Set-Content $lock
    Run 'corrupt-default-lock' @('restore',$app,'--locked-mode','--force') $false
    Run 'release-independent-of-debug' @('restore',$app,'--locked-mode','--force','-p:Configuration=Release')
    $releaseLock = "$fixture/eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json"
    $releaseValid = Get-Content $releaseLock -Raw
    Move-Item -LiteralPath $releaseLock -Destination "$out/saved-release-lock.json"
    Run 'missing-release-lock' @('restore',$app,'--locked-mode','--force','-p:Configuration=Release') $false
    'deliberately invalid F2 release lock' | Set-Content $releaseLock
    Run 'corrupt-release-lock' @('restore',$app,'--locked-mode','--force','-p:Configuration=Release') $false
    Set-Content $releaseLock $releaseValid
    Set-Content $lock $valid
    Run 'explicit-debug-restore' @('restore',$app,'--locked-mode','--force','-p:Configuration=Debug')
    Check 'no-unconfigured-locks' (@(Get-ChildItem "$fixture/eng/prism/locks" -File).Count -eq 0) $null
    Run 'foreign-after' (@('msbuild',$foreign,$propertyQuery) + $foreignArgs)
    Check 'foreign-properties-unchanged' ((Get-Content "$out/foreign-before.log" -Raw) -ceq (Get-Content "$out/foreign-after.log" -Raw)) $null
    Run 'foreign-restore' (@('restore',$foreign) + $foreignArgs)
    Check 'foreign-lock-not-written-to-integration' (@(Get-ChildItem "$fixture/eng/prism/locks" -Recurse -Filter OtherApplication.lock.json).Count -eq 0) $null
    Run 'foreign-name-collision' (@('msbuild',"$out/foreign/Prism.Events.csproj",'-getItem:PackageReference') + $foreignArgs)
    Check 'foreign-no-security-pin' (!(Get-Content "$out/foreign-name-collision.log" -Raw).Contains('Microsoft.Build.Tasks.Git')) $null
    . "$fixture/eng/prism/Leave-Prism.ps1"
    Check 'leave-restores-hooks' (!$env:CustomBeforeDirectoryBuildProps -and !$env:CustomAfterDirectoryBuildProps) $null
    foreach ($name in @('CustomBeforeDirectoryBuildProps','CustomAfterDirectoryBuildProps')) {
        [Environment]::SetEnvironmentVariable($name,'foreign-hook-preserved','Process')
        $failed = $false
        try { . "$fixture/eng/prism/Enter-Prism.ps1" } catch { $failed = $true; $_ | Out-String | Set-Content "$out/conflict-$name.log" }
        Check "conflict-$name-preserved" ($failed -and [Environment]::GetEnvironmentVariable($name,'Process') -eq 'foreign-hook-preserved') $null
        [Environment]::SetEnvironmentVariable($name,$null,'Process')
    }
    foreach ($name in @('DISABLE_GITVERSIONING','GeneratePackageOnBuild','NuGetAudit','NuGetAuditMode','NUGET_PACKAGES','CustomAfterMicrosoftCommonProps')) {
        [Environment]::SetEnvironmentVariable($name,'caller-value','Process')
    }
    . "$fixture/eng/prism/Enter-Prism.ps1"
    foreach ($name in @('DISABLE_GITVERSIONING','GeneratePackageOnBuild','NuGetAudit','NuGetAuditMode','NUGET_PACKAGES','CustomAfterMicrosoftCommonProps')) {
        Check "preserves-$name" ([Environment]::GetEnvironmentVariable($name,'Process') -eq 'caller-value') $null
        [Environment]::SetEnvironmentVariable($name,$null,'Process')
    }
    . "$fixture/eng/prism/Leave-Prism.ps1"
    foreach ($mode in @('none','props-only','targets-only')) {
        $env:CustomBeforeDirectoryBuildProps = if ($mode -eq 'props-only') { "$fixture/eng/prism/Lock.props" } else { $null }
        $env:CustomAfterDirectoryBuildProps = if ($mode -eq 'targets-only') { "$fixture/eng/prism/PrismSecurity.targets" } else { $null }
        Run "guard-$mode" @('restore',$app,'--locked-mode','--force') $false
    }
    $env:CustomBeforeDirectoryBuildProps = $null
    $env:CustomAfterDirectoryBuildProps = $null
    $locationBeforeBuild = (Get-Location).Path
    $buildFailed = $false
    try { & "$fixture/eng/prism/Build.ps1" *> "$out/build-failure-restores-environment.log" } catch { $buildFailed = $true }
    Check 'build-failure-restores-environment' ($buildFailed -and !$env:CustomBeforeDirectoryBuildProps -and !$env:CustomAfterDirectoryBuildProps -and (Get-Location).Path -eq $locationBeforeBuild) $null
    'ordinary marker' | Set-Content "$checkout/R1Untracked.txt"
    Prepare 'prepare-untracked' $false
    Remove-Item -LiteralPath "$checkout/R1Untracked.txt"
    & git -C $checkout remote set-url origin https://example.invalid/prism.git
    Prepare 'prepare-wrong-origin' $false
    & git -C $checkout remote set-url origin https://github.com/PrismLibrary/Prism.git
    New-Item -ItemType Directory -Force "$checkout/src/Prism.Events/Debug" | Out-Null
    'namespace F2 { internal sealed class R1IgnoredSource {} }' | Set-Content "$checkout/src/Prism.Events/Debug/R1IgnoredSource.cs"
    Prepare 'prepare-ignored-source' $false
    Remove-Item -LiteralPath "$checkout/src/Prism.Events/Debug/R1IgnoredSource.cs"
    New-Item -ItemType Directory -Force "$checkout/src/Prism.Events/obj", "$checkout/src/Prism.Events/bin" | Out-Null
    'output marker' | Set-Content "$checkout/src/Prism.Events/bin/f2-output.txt"
    'namespace F2 { internal sealed class Unapproved {} }' | Set-Content "$checkout/src/Prism.Events/obj/unapproved.cs"
    Prepare 'prepare-normal-output-roots' $true
    # Default SDK globs exclude obj; an explicit inclusion must still be rejected before compilation.
    "$checkout/src/Prism.Events/obj/unapproved.cs" | Set-Content "$out/compile-inputs.txt"
    '' | Set-Content "$out/generated-inputs.txt"
    & pwsh -NoProfile -File "$fixture/eng/prism/Prepare-Prism.ps1" -VerifyOnly -CompileInputsFile "$out/compile-inputs.txt" -GeneratedInputsFile "$out/generated-inputs.txt" -ProjectDirectory "$checkout/src/Prism.Events" *> "$out/compile-unapproved-obj.log"
    Check 'compile-unapproved-obj' ($LASTEXITCODE -ne 0) "exit=$LASTEXITCODE"
    # Empty checkout in a separate fixture tests failure without changing any official file.
    $broken = "$out/broken"
    New-Item -ItemType Directory -Force "$broken/eng/prism", "$broken/.deps/prism" | Out-Null
    Copy-Item "$PSScriptRoot/Prepare-Prism.ps1" "$broken/eng/prism"
    & pwsh -NoProfile -File "$broken/eng/prism/Prepare-Prism.ps1" *> "$out/prepare-broken.log"
    Check 'prepare-broken' ($LASTEXITCODE -ne 0) "exit=$LASTEXITCODE"
} finally {
    if ($global:CwbPrismEnvironment) { . "$fixture/eng/prism/Leave-Prism.ps1" }
    foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
}
$results | Format-Table name,passed



