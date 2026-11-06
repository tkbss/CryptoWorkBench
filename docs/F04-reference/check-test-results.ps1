param(
    [string]$ResultsDirectory = 'artifacts/F04-corrections',
    [string]$Report = 'docs/F04-implementation-report.md'
)
$ErrorActionPreference = 'Stop'
$reportText = Get-Content -LiteralPath $Report -Raw
foreach ($suite in @('core', 'gui')) {
    [xml]$trx = Get-Content -LiteralPath (Join-Path $ResultsDirectory "$suite.trx") -Raw
    $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($results.Count -eq 0) { throw "No test results for $suite." }
    $passed = @($results | Where-Object outcome -eq 'Passed').Count
    $skipped = @($results | Where-Object outcome -eq 'NotExecuted').Count
    $failed = @($results | Where-Object outcome -eq 'Failed').Count
    if ($passed + $skipped + $failed -ne $results.Count -or $failed -ne 0) {
        throw "Unsuccessful or unexpected outcomes in $suite."
    }
    $line = "F04-CORRECTIONS-${suite}: passed=$passed skipped=$skipped failed=$failed total=$($results.Count)"
    if (-not ($reportText -split '\r?\n' -ccontains $line)) {
        throw "Report does not match TRX: $line"
    }
    $line
}
