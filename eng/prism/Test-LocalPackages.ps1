#requires -Version 7.4
[CmdletBinding()]
param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/prism-package-check-'+[Guid]::NewGuid().ToString('N'))}
New-Item -ItemType Directory -Path $EvidenceDirectory -Force|Out-Null
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-digests.json') -Raw|ConvertFrom-Json
$results=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[string[]]$Arguments,[string]$FailurePattern=''){
    $log=Join-Path $evidence ($Name+'.log')
    & dotnet @Arguments *> $log
    $code=$LASTEXITCODE
    $text=Get-Content -LiteralPath $log -Raw
    if($FailurePattern){if($code -eq 0 -or $text -notmatch $FailurePattern){throw "Expected controlled failure missing: $Name; see $log"}}
    elseif($code){throw "Unexpected failure: $Name; see $log"}
    $results.Add([ordered]@{test=$Name;passed=$true;exitCode=$code;expectedFailure=[bool]$FailurePattern})
}
Push-Location $repo
try{
    foreach($name in 'CustomBeforeDirectoryBuildProps','CustomAfterDirectoryBuildProps'){
        if([Environment]::GetEnvironmentVariable($name,'Process')){throw 'Run package tests without Prism environment hooks.'}
    }
    $project='CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj'
    Check 'locked-restore' @('restore',$project,'--locked-mode','-p:Configuration=Debug')
    $p=$manifest.packages[0]
    $archive=Join-Path $repo ('.packages/prism/'+$p.file)
    $bytes=[IO.File]::ReadAllBytes($archive)
    try {
        # Rename a single known fixture file; never remove a cache or source tree.
        $backup=$archive+'.test-backup'
        if(Test-Path -LiteralPath $backup){throw 'Existing package backup; inspect before retry.'}
        Move-Item -LiteralPath $archive -Destination $backup
        try {Check 'missing-feed-with-warm-cache' @('restore',$project,'--locked-mode') 'Local Prism package missing'}
        finally {Move-Item -LiteralPath $backup -Destination $archive}
        $altered=[byte[]]$bytes.Clone();$altered[$altered.Length-1]=$altered[$altered.Length-1] -bxor 1
        [IO.File]::WriteAllBytes($archive,$altered)
        Check 'altered-feed-with-warm-cache' @('restore',$project,'--locked-mode') 'Local Prism package digest mismatch'
    }finally{[IO.File]::WriteAllBytes($archive,$bytes)}
    # Resolve actual NuGet assets, including a caller-selected cache, rather than guessing cache paths.
    $assets=Get-Content (Join-Path $repo 'CryptoWorkBenchAvalonia/obj/project.assets.json') -Raw|ConvertFrom-Json -AsHashtable
    $library=$assets.libraries[$p.id+'/'+$p.version].path
    $cacheFile=$null
    foreach($folder in $assets.packageFolders.Keys){
        $candidate=Join-Path $folder ($library+'/lib/net10.0/'+$p.assembly)
        if(Test-Path -LiteralPath $candidate){$cacheFile=$candidate;break}
    }
    if(!$cacheFile){throw 'Resolved assembly not found.'}
    $dllBytes=[IO.File]::ReadAllBytes($cacheFile)
    try{
        $altered=[byte[]]$dllBytes.Clone();$altered[$altered.Length-1]=$altered[$altered.Length-1] -bxor 1
        [IO.File]::WriteAllBytes($cacheFile,$altered)
        Check 'altered-cached-assembly' @('msbuild',$project,'-t:ResolveReferences','-p:Configuration=Debug') 'Resolved Prism assembly differs'
    }finally{[IO.File]::WriteAllBytes($cacheFile,$dllBytes)}
    Check 'recovered-inputs' @('restore',$project,'--locked-mode','-p:Configuration=Debug')
    # NuGet/runtime graph must not contain any official counterpart of the four temporary assemblies.
    foreach($name in $assets.libraries.Keys){
        if($name -match '^(Prism.Core|Prism.Events|Prism.Avalonia|Prism.DryIoc.Avalonia)/'){throw "Duplicate official package: $name"}
    }
    foreach($p in $manifest.packages){
        $package=Join-Path $repo ('.packages/prism/'+$p.file)
        if((Get-FileHash -LiteralPath $package).Hash -ne $p.sha256){throw 'Package was not restored after probes.'}
    }
    $results.Add([ordered]@{test='unique-prism-package-graph';passed=$true})
    $results|ConvertTo-Json -Depth 5|Set-Content (Join-Path $evidence 'results.json') -Encoding utf8
    Write-Host "Local package checks passed; evidence: $evidence"
}finally{Pop-Location}
