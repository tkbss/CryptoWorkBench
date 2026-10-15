[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory)]
    [string] $PublishDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$publishRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sourceDocsRoot = Join-Path $repositoryRoot 'CryptoScript/InfoDocs'
$appName = 'CryptoWorkBenchAvalonia.Desktop'

# Ordinal comparisons catch incorrect casing even on Windows and default macOS
# filesystems. All lookups are relative to the deployed application directory.
$publishedFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($file in Get-ChildItem -LiteralPath $publishRoot -File -Recurse) {
    $relativePath = [IO.Path]::GetRelativePath($publishRoot, $file.FullName).Replace('\', '/')
    [void] $publishedFiles.Add($relativePath)
}

function Assert-PublishedFile([string] $RelativePath) {
    if (-not $publishedFiles.Contains($RelativePath)) {
        throw "Missing publish file (exact casing required): $RelativePath"
    }
    $path = Join-Path $publishRoot $RelativePath
    if ((Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Empty publish file: $RelativePath"
    }
}

$executable = if ($RuntimeIdentifier -eq 'win-x64') { "$appName.exe" } else { $appName }
foreach ($file in @($executable, "$appName.dll", "$appName.deps.json",
        "$appName.runtimeconfig.json", 'CryptoWorkBenchAvalonia.dll', 'CryptoScript.dll')) {
    Assert-PublishedFile $file
}
if ($RuntimeIdentifier -ne 'win-x64') {
    & test -x (Join-Path $publishRoot $executable)
    if ($LASTEXITCODE -ne 0) { throw "Apphost is not executable: $executable" }
}

$runtimeConfig = Get-Content -LiteralPath (Join-Path $publishRoot "$appName.runtimeconfig.json") -Raw |
    ConvertFrom-Json -AsHashtable
$framework = $runtimeConfig.runtimeOptions.framework
if ($framework.name -ne 'Microsoft.NETCore.App' -or $framework.version -notlike '10.0.*') {
    throw 'Publish must require the shared .NET 10 runtime (framework-dependent).'
}

$deps = Get-Content -LiteralPath (Join-Path $publishRoot "$appName.deps.json") -Raw |
    ConvertFrom-Json -AsHashtable
$targetName = $deps.runtimeTarget.name
if (-not $targetName.EndsWith("/$RuntimeIdentifier", [StringComparison]::Ordinal)) {
    throw "Unexpected dependency target: $targetName"
}

# RID-specific publish flattens managed/native assets into the app directory;
# satellite resources retain their locale subdirectory. Check the SDK manifest
# rather than maintaining a second list of NuGet dependency versions.
$nativeCount = 0
foreach ($library in $deps.targets[$targetName].Values) {
    foreach ($kind in @('runtime', 'native', 'resources')) {
        if (-not $library.ContainsKey($kind)) { continue }
        foreach ($asset in $library[$kind].GetEnumerator()) {
            $fileName = ($asset.Key -split '/')[-1]
            if ($fileName -eq '_._') { continue }
            $relativePath = if ($kind -eq 'resources') {
                "$($asset.Value.locale)/$fileName"
            } else { $fileName }
            Assert-PublishedFile $relativePath
            if ($kind -eq 'native') { $nativeCount++ }
        }
    }
}
if ($nativeCount -eq 0) { throw 'No platform-native dependencies were validated.' }

$sourceDocs = @(Get-ChildItem -LiteralPath $sourceDocsRoot -File -Recurse)
if ($sourceDocs.Count -eq 0) { throw 'Source InfoDocs directory is empty.' }
foreach ($source in $sourceDocs) {
    $relativePath = 'InfoDocs/' + [IO.Path]::GetRelativePath($sourceDocsRoot, $source.FullName).Replace('\', '/')
    Assert-PublishedFile $relativePath
    $deployed = Join-Path $publishRoot $relativePath
    if ((Get-FileHash -LiteralPath $source.FullName).Hash -ne (Get-FileHash -LiteralPath $deployed).Hash) {
        throw "Published documentation differs from source: $relativePath"
    }
    # Exercise readability from the deployed layout used by FileInfoDocumentationProvider.
    [void] [IO.File]::ReadAllText($deployed)
}

Write-Host "Validated $RuntimeIdentifier apphost, managed dependencies, $nativeCount native assets and $($sourceDocs.Count) InfoDocs files."
Write-Host 'This validates publish contents; it does not launch or smoke-test the graphical application.'
