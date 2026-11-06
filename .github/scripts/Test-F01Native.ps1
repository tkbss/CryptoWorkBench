[CmdletBinding()]
param([switch] $RequireMacArm64)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($RequireMacArm64 -and (-not $IsMacOS -or
        [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'Arm64')) {
    throw 'This gate requires a real macOS ARM64 process; emulated/x64 results do not qualify.'
}
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$previousArchitectureRequirement = $env:CWE_REQUIRE_MAC_ARM64
Push-Location $repositoryRoot
try {
    $env:CWE_REQUIRE_MAC_ARM64 = if ($RequireMacArm64) { '1' } else { '0' }
    $resultPath = Join-Path $repositoryRoot 'artifacts/f01-native/F01-native.trx'
    if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath }
    # Includes native operations, numbering, failure cleanup, symlinks and the exact container vector.
    # Blame records the started-test sequence and captures a dump if a test stops progressing.
    dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --configuration Release --no-build --no-restore `
        --filter 'FullyQualifiedName~FileEncryptionTests|FullyQualifiedName~FilePublicationNativeTests' `
        --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=F01-native.trx' `
        --results-directory artifacts/f01-native --diag artifacts/f01-native/vstest.log `
        --blame-hang --blame-hang-timeout 60s --blame-hang-dump-type mini `
        -- NUnit.InternalTraceLevel=Verbose
    if ($LASTEXITCODE -ne 0) { throw "F01 native gate failed: exit $LASTEXITCODE" }
    [xml] $results = Get-Content -LiteralPath $resultPath -Raw
    $cases = @($results.TestRun.Results.UnitTestResult)
    if (-not ($cases | Where-Object { $_.testName -eq 'NistVectorProducesExactContainer' -and $_.outcome -eq 'Passed' })) {
        throw 'The container reference vector did not pass (or was not discovered).'
    }
    if ($IsMacOS) {
        $nativeIds = @($results.TestRun.TestDefinitions.UnitTest |
            Where-Object { $_.TestMethod.className -eq 'CryptoScriptUnitTest.FilePublicationNativeTests' } |
            ForEach-Object { $_.id })
        $nativePassed = @($cases | Where-Object { $_.testId -in $nativeIds -and $_.outcome -eq 'Passed' })
        if ($nativeIds.Count -lt 7 -or $nativePassed.Count -ne $nativeIds.Count) {
            throw 'Every Darwin native ABI regression must be discovered and pass.'
        }
    }
}
finally {
    $env:CWE_REQUIRE_MAC_ARM64 = $previousArchitectureRequirement
    Pop-Location
}
