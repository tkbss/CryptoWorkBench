#requires -Version 7.0
[CmdletBinding()]
param(
    [switch]$VerifyOnly,
    [string]$CompileInputsFile,
    [string]$GeneratedInputsFile,
    [string]$ProjectDirectory
)
$ErrorActionPreference = 'Stop'
$pin = '358118cd640d9a22ff8cf21c8ad197fa038b7990'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$checkout = Join-Path $repo '.deps/prism'
function Invoke-PrismGit([string[]]$Arguments) {
    $result = & git @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Prism Git operation failed: $Arguments" }
    $result
}
if (-not (Test-Path -LiteralPath $checkout)) {
    if ($VerifyOnly) { throw 'Prism checkout is missing. Run Enter-Prism.ps1 first.' }
    Invoke-PrismGit @('init', $checkout)
    Invoke-PrismGit @('-C', $checkout, 'config', 'core.autocrlf', 'false')
    Invoke-PrismGit @('-C', $checkout, 'remote', 'add', 'origin', 'https://github.com/PrismLibrary/Prism.git')
    Invoke-PrismGit @('-C', $checkout, 'fetch', '--depth', '1', 'origin', $pin)
    Invoke-PrismGit @('-C', $checkout, 'checkout', '--detach', $pin)
}
$actual = Invoke-PrismGit @('-C', $checkout, 'rev-parse', 'HEAD')
if ($actual -ne $pin) { throw 'Unexpected Prism checkout. Existing directories are never reset or overwritten.' }
$origin = Invoke-PrismGit @('-C', $checkout, 'remote', 'get-url', 'origin')
if ($origin -ne 'https://github.com/PrismLibrary/Prism.git') { throw 'Unexpected Prism origin.' }
$dirty = Invoke-PrismGit @('-C', $checkout, 'status', '--porcelain', '--untracked-files=all')
if ($dirty) { throw 'Prism sources must be unchanged. Existing changes are never discarded.' }

# Compare disk content to the pinned tree, independently of index flags or local Git filters.
$tree = @(Invoke-PrismGit @('-C', $checkout, '-c', 'core.quotePath=false', 'ls-tree', '-r', $pin))
$tracked = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
foreach ($entry in $tree) {
    if ($entry -notmatch '^100(?:644|755) blob ([0-9a-f]{40})\t(.+)$') { throw "Unsupported Prism tree entry: $entry" }
    $tracked.Add($Matches[2], $Matches[1])
}
$paths = @($tracked.Keys | Sort-Object)
$hashes = @($paths | & git -C $checkout hash-object --no-filters --stdin-paths)
if ($LASTEXITCODE -ne 0 -or $hashes.Count -ne $paths.Count) { throw 'Cannot hash all pinned Prism files.' }
for ($i = 0; $i -lt $paths.Count; $i++) {
    if ($hashes[$i] -eq $tracked[$paths[$i]]) { continue }
    # The official text=auto attributes permit CRLF checkout bytes. Accept only
    # this byte-preserving newline normalization, never a configurable clean filter.
    $bytes = [IO.File]::ReadAllBytes((Join-Path $checkout $paths[$i]))
    $encoding = [Text.Encoding]::GetEncoding(28591)
    $normalized = $encoding.GetBytes($encoding.GetString($bytes).Replace("`r`n", "`n"))
    $header = [Text.Encoding]::ASCII.GetBytes("blob $($normalized.Length)`0")
    $stream = [IO.MemoryStream]::new()
    try {
        $stream.Write($header)
        $stream.Write($normalized)
        $stream.Position = 0
        $canonicalHash = (Get-FileHash -InputStream $stream -Algorithm SHA1).Hash.ToLowerInvariant()
    } finally { $stream.Dispose() }
    if ($canonicalHash -ne $tracked[$paths[$i]]) { throw "Prism source differs from pinned tree: $($paths[$i])" }
}
# Only the four actual project output roots are exempt. In particular Debug/ and
# WPF linked-source subtrees are NOT outputs, even when upstream Git ignores them.
$outputRoots = @('src/Prism.Core', 'src/Prism.Events', 'src/Avalonia/Prism.Avalonia', 'src/Avalonia/Prism.DryIoc.Avalonia')
$ignored = @(Invoke-PrismGit @('-C', $checkout, '-c', 'core.quotePath=false', 'ls-files', '--others', '--ignored', '--exclude-standard'))
foreach ($path in $ignored) {
    $allowed = $false
    foreach ($root in $outputRoots) {
        if ($path.StartsWith("$root/bin/", [StringComparison]::Ordinal) -or $path.StartsWith("$root/obj/", [StringComparison]::Ordinal)) { $allowed = $true; break }
    }
    if (!$allowed) { throw "Unexpected ignored Prism input: $path" }
}
# No junction/symlink may redirect an approved source/output tree elsewhere.
$links = @(Get-Item -LiteralPath $checkout) + @(Get-ChildItem -LiteralPath $checkout -Force -Recurse -Attributes ReparsePoint)
foreach ($item in $links) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Prism path must not be a link: $($item.FullName)" }
}
if ($CompileInputsFile) {
    if (!$GeneratedInputsFile -or !$ProjectDirectory) { throw 'Compile input validation requires generated inputs and project directory.' }
    $projectRelative = [IO.Path]::GetRelativePath($checkout, [IO.Path]::GetFullPath($ProjectDirectory)).Replace('\', '/')
    if ($projectRelative -cnotin $outputRoots) { throw 'Compile validation requested for an unexpected Prism project.' }
    $comparison = if ($IsWindows) { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }
    $generated = [Collections.Generic.HashSet[string]]::new($comparison)
    foreach ($path in Get-Content -LiteralPath $GeneratedInputsFile) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $full = [IO.Path]::GetFullPath($path, $ProjectDirectory)
        $relative = [IO.Path]::GetRelativePath($ProjectDirectory, $full).Replace('\', '/')
        if (!$relative.StartsWith('obj/', [StringComparison]::Ordinal)) { throw "Generated Prism input must be under the project obj directory: $full" }
        [void]$generated.Add($full)
    }
    foreach ($path in Get-Content -LiteralPath $CompileInputsFile) {
        $full = [IO.Path]::GetFullPath($path, $ProjectDirectory)
        $relative = [IO.Path]::GetRelativePath($checkout, $full).Replace('\', '/')
        if (!$tracked.ContainsKey($relative) -and !$generated.Contains($full)) { throw "Unapproved Prism Compile input: $full" }
    }
}
Write-Host "Verified official Prism $pin ($($tracked.Count) pinned files) in $checkout"



