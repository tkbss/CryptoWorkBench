param([string]$DependencyDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'g03-java'))
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$classes = Join-Path $root 'CryptoScriptUnitTest/obj/g03-java'
foreach ($line in Get-Content (Join-Path $PSScriptRoot 'bc-java/SHA256SUMS.txt')) {
    $parts = $line.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Length -ne 2 -or $parts[1] -ne [IO.Path]::GetFileName($parts[1])) { throw 'Invalid source manifest' }
    if ((Get-FileHash (Join-Path $PSScriptRoot "bc-java/$($parts[1])") -Algorithm SHA256).Hash -ne $parts[0]) {
        throw "Pinned Java source hash mismatch: $($parts[1])"
    }
}
New-Item -ItemType Directory -Force $DependencyDirectory, $classes | Out-Null
$dependencies = @(
    @('bcprov.jar', 'https://repo.maven.apache.org/maven2/org/bouncycastle/bcprov-jdk18on/1.83/bcprov-jdk18on-1.83.jar', '82CF3A2AF766C3BC874F6D36B9F20A8B99A8F09762DC776E8A227A45D8DAAAFB'),
    @('junit.jar', 'https://repo.maven.apache.org/maven2/junit/junit/4.13.2/junit-4.13.2.jar', '8E495B634469D64FB8ACFA3495A065CBACC8A0FFF55CE1E31007BE4C16DC57D3')
)
foreach ($dependency in $dependencies) {
    $path = Join-Path $DependencyDirectory $dependency[0]
    if (!(Test-Path -LiteralPath $path)) { Invoke-WebRequest $dependency[1] -OutFile $path }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $dependency[2]) { throw "Dependency hash mismatch: $path" }
}
$cp = (Join-Path $DependencyDirectory 'bcprov.jar') + [IO.Path]::PathSeparator + (Join-Path $DependencyDirectory 'junit.jar')
$sources = (Get-ChildItem (Join-Path $PSScriptRoot 'bc-java/*.java')).FullName
& javac -cp $cp -d $classes $sources (Join-Path $PSScriptRoot 'G03Reference.java')
if ($LASTEXITCODE -ne 0) { throw 'javac failed' }
$cp = $classes + [IO.Path]::PathSeparator + $cp
& java -cp $cp org.bouncycastle.crypto.threshold.test.ShamirSecretSplitterTest
if ($LASTEXITCODE -ne 0) { throw 'Original Java tests failed' }
$actual = & java -cp $cp org.bouncycastle.crypto.threshold.G03Reference generate
if ($LASTEXITCODE -ne 0) { throw 'Java vector generation failed' }
$expected = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'java-vectors.txt')
if (($actual -join "`n") -cne ($expected -join "`n")) { throw 'Pinned Java vectors differ' }
Push-Location $root
try {
    & dotnet test CryptoScriptUnitTest --filter FullyQualifiedName~SecretSharingTests --logger 'trx;LogFileName=g03-reference.trx' --results-directory docs/architecture/evidence/G03
    if ($LASTEXITCODE -ne 0) { throw 'C# reference tests failed' }
    $vectors = Join-Path $root 'CryptoScriptUnitTest/bin/Debug/net10.0/g03-csharp-vectors.txt'
    & java -cp $cp org.bouncycastle.crypto.threshold.G03Reference verify $vectors
    if ($LASTEXITCODE -ne 0) { throw 'C# -> Java cross-tests failed' }
} finally { Pop-Location }
