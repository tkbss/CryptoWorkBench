#requires -Version 7.4
[CmdletBinding()]
param(
    [string]$FeedDirectory,
    [switch]$Rebuild,
    # Explicit maintenance only: produces a candidate digest manifest, never updates reviewed inputs.
    [string]$CandidateManifest
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$spec=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'packages.json') -Raw|ConvertFrom-Json
$reviewed=Join-Path $PSScriptRoot 'package-digests.json'
if(!$FeedDirectory){$FeedDirectory=Join-Path $repo '.packages/prism'}
$feed=[IO.Path]::GetFullPath($FeedDirectory)
if($feed -match '^https?://'){throw 'Only local package directories are supported.'}
$sdk=& dotnet --version
if($LASTEXITCODE -ne 0 -or $sdk.Trim() -ne '10.0.401'){throw 'SDK 10.0.401 required.'}
if(!$CandidateManifest -and !(Test-Path -LiteralPath $reviewed)){throw 'Reviewed package digests missing.'}
if(!$Rebuild -and !$CandidateManifest -and (Test-Path -LiteralPath $feed)){
    $expected=Get-Content -LiteralPath $reviewed -Raw|ConvertFrom-Json
    $valid=$true
    foreach($p in $expected.packages){
        $file=Join-Path $feed $p.file
        if(!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file).Hash -ne $p.sha256){$valid=$false;break}
    }
    if($valid){Write-Host 'Reviewed local Prism feed already complete; no source restore or rebuild.';return}
    throw 'Existing local feed is incomplete or altered. Use a new FeedDirectory or explicitly request -Rebuild after inspecting it.'
}
# Hooks belong ONLY to this packaging process and are restored even on failure.
$names=@('CustomBeforeDirectoryBuildProps','CustomAfterDirectoryBuildProps')
$previous=@{}
foreach($name in $names){
    $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
    if(![string]::IsNullOrEmpty($previous[$name])){throw "Start bootstrap in a clean shell; existing hook: $name"}
}
& (Join-Path $PSScriptRoot 'Prepare-Prism.ps1')
$checkout=Join-Path $repo '.deps/prism-packaging'
$staging=Join-Path $repo ('.packages/staging-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force|Out-Null
try {
    $env:CustomBeforeDirectoryBuildProps=Join-Path $PSScriptRoot 'Bootstrap.Lock.props'
    $env:CustomAfterDirectoryBuildProps=Join-Path $PSScriptRoot 'Bootstrap.Security.targets'
    $project=Join-Path $checkout $spec.packages[-1].project
    & dotnet restore $project --locked-mode -p:Configuration=Release --configfile (Join-Path $PSScriptRoot 'Bootstrap-NuGet.Config')
    if($LASTEXITCODE){throw 'Prism source locked restore failed.'}
    # Bootstrap.Lock.props fixes virtual source paths and copyright year.
    $target=if($Rebuild){'Rebuild'}else{'Build'}
    & dotnet build $project -c Release -f net10.0 --no-restore "-t:$target"
    if($LASTEXITCODE){throw 'Prism net10.0 build failed.'}
    & (Join-Path $PSScriptRoot 'Prepare-Prism.ps1') -VerifyOnly
} finally {
    foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
}
Add-Type -AssemblyName System.IO.Compression
$results=@()
foreach($p in $spec.packages){
    $projectDir=Split-Path (Join-Path $checkout $p.project)
    $dll=Join-Path $projectDir ("bin/Release/net10.0/"+$p.assembly+'.dll')
    $identity=[Reflection.AssemblyName]::GetAssemblyName($dll)
    if($identity.Name -cne $p.assembly){throw 'Unexpected assembly identity.'}
    $dependencies=($p.dependencies.PSObject.Properties|Sort-Object Name|ForEach-Object {
        '<dependency id="'+$_.Name+'" version="'+[Security.SecurityElement]::Escape([string]$_.Value)+'" />'
    }) -join ''
    $nuspec='<?xml version="1.0" encoding="utf-8"?><package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>'+ $p.id +'</id><version>'+ $spec.version +'</version><authors>Prism Software, LLC</authors><requireLicenseAcceptance>true</requireLicenseAcceptance><license type="file">LICENSE</license><description>Temporary local CryptoWorkBench packaging of unchanged pinned Prism. Not an official Prism package. No external publication.</description><repository type="git" url="https://github.com/PrismLibrary/Prism" commit="'+$spec.commit+'"/><dependencies><group targetFramework="net10.0">'+$dependencies+'</group></dependencies></metadata></package>'
    # Canonical ZIP: ordinal entry order, fixed timestamp, no compression variation.
    $entries=[Collections.Generic.SortedDictionary[string,byte[]]]::new([StringComparer]::Ordinal)
    $entries.Add($p.id+'.nuspec',[Text.Encoding]::UTF8.GetBytes($nuspec))
    $entries.Add('LICENSE',[IO.File]::ReadAllBytes((Join-Path $checkout 'LICENSE')))
    $entries.Add('PROVENANCE.txt',[Text.Encoding]::UTF8.GetBytes(([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Package-Provenance.txt'))).Replace("`r`n","`n")))
    $entries.Add('lib/net10.0/'+$p.assembly+'.dll',[IO.File]::ReadAllBytes($dll))
    $entries.Add('[Content_Types].xml',[Text.Encoding]::UTF8.GetBytes('<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="nuspec" ContentType="application/octet"/><Default Extension="dll" ContentType="application/octet"/><Default Extension="txt" ContentType="text/plain"/><Override PartName="/LICENSE" ContentType="text/plain"/></Types>'))
    $file=$p.id.ToLowerInvariant()+'.'+$spec.version+'.nupkg'
    $archive=[IO.Compression.ZipFile]::Open((Join-Path $staging $file),[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach($entry in $entries.GetEnumerator()){
            $item=$archive.CreateEntry($entry.Key,[IO.Compression.CompressionLevel]::NoCompression)
            $item.LastWriteTime=[DateTimeOffset]::new(2026,1,1,0,0,0,[TimeSpan]::Zero)
            $item.ExternalAttributes=0
            $stream=$item.Open()
            try{$stream.Write($entry.Value)}finally{$stream.Dispose()}
        }
    }finally{$archive.Dispose()}
    # ZipArchive stamps the host OS in the central directory even for stored entries.
    # These small archives have no comment or ZIP64 records. Keep Windows creator ID 0.
    $archivePath=Join-Path $staging $file
    $bytes=[IO.File]::ReadAllBytes($archivePath)
    $end=$bytes.Length-22
    if([BitConverter]::ToUInt32($bytes,$end) -ne 0x06054b50){throw 'Unexpected ZIP trailer.'}
    $offset=[BitConverter]::ToInt32($bytes,$end+16)
    foreach($entry in $entries.Keys){
        if([BitConverter]::ToUInt32($bytes,$offset) -ne 0x02014b50){throw 'Unexpected ZIP directory entry.'}
        $bytes[$offset+5]=0
        $offset+=46+[BitConverter]::ToUInt16($bytes,$offset+28)+[BitConverter]::ToUInt16($bytes,$offset+30)+[BitConverter]::ToUInt16($bytes,$offset+32)
    }
    if($offset -ne $end){throw 'Unexpected ZIP central directory length.'}
    [IO.File]::WriteAllBytes($archivePath,$bytes)
    $results+= [ordered]@{id=$p.id;version=$spec.version;file=$file;sha256=(Get-FileHash -LiteralPath (Join-Path $staging $file)).Hash.ToLowerInvariant();assembly=$p.assembly+'.dll';assemblyIdentity=$identity.FullName;assemblySha256=(Get-FileHash -LiteralPath $dll).Hash.ToLowerInvariant()}
}
$manifest=[ordered]@{commit=$spec.commit;sdk='10.0.401';targetFramework='net10.0';configuration='Release';packages=$results}
if($CandidateManifest){
    $manifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $CandidateManifest -Encoding utf8
}else{
    $expected=Get-Content -LiteralPath $reviewed -Raw|ConvertFrom-Json
    if($expected.commit -ne $spec.commit -or $expected.packages.Count -ne $results.Count){throw 'Reviewed manifest mismatch.'}
    foreach($p in $results){
        $e=@($expected.packages|Where-Object id -eq $p.id)
        if($e.Count -ne 1 -or $e[0].sha256 -ne $p.sha256 -or $e[0].assemblySha256 -ne $p.assemblySha256){
            foreach($actual in $results){
                Write-Warning "$($actual.id) archive=$($actual.sha256) assembly=$($actual.assemblySha256)"
            }
            throw "Reproduction mismatch for $($p.id). Candidate packages remain in $staging; feed unchanged."
        }
    }
}
New-Item -ItemType Directory -Path $feed -Force|Out-Null
foreach($p in $results){Copy-Item -LiteralPath (Join-Path $staging $p.file) -Destination (Join-Path $feed $p.file)}
Write-Host "Created four local Prism packages in $feed; source files unchanged."
