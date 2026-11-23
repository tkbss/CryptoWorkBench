#requires -Version 7.4
# Normalize generated compiler inputs to the reviewed Windows format.
param([Parameter(Mandatory)][string]$IntermediateDirectory,[Parameter(Mandatory)][string]$AssemblyInfo)
$ErrorActionPreference = 'Stop'
foreach ($file in Get-ChildItem -LiteralPath $IntermediateDirectory -Filter '*.resources' -File) {
    $reader = [Resources.ResourceReader]::new($file.FullName)
    $buffer = [IO.MemoryStream]::new()
    $writer = [Resources.ResourceWriter]::new($buffer)
    try {
        foreach ($entry in $reader) {
            if ($entry.Value -isnot [string]) { throw "Unexpected non-string resource in $($file.Name)." }
            $writer.AddResource($entry.Key, $entry.Value.ReplaceLineEndings("`r`n"))
        }
        $writer.Generate()
        $bytes = $buffer.ToArray()
    } finally {
        $reader.Dispose()
        $writer.Dispose()
        $buffer.Dispose()
    }
    [IO.File]::WriteAllBytes($file.FullName, $bytes)
}
# Avalonia's assembly description contains a multiline verbatim string.
$source = [IO.File]::ReadAllText($AssemblyInfo)
[IO.File]::WriteAllText($AssemblyInfo, $source.ReplaceLineEndings("`r`n"))
