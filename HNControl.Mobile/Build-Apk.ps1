param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\entrega'),
    [string]$SigningDirectory = (Join-Path $env:LOCALAPPDATA 'HNControlSigning')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'HNControl.Mobile.csproj'
$publishDirectory = Join-Path $env:LOCALAPPDATA 'HNControlBuild\publish-release'
$keystore = Join-Path $SigningDirectory 'hncontrol-release.jks'
$passwordFile = Join-Path $SigningDirectory 'keystore-password.txt'
$releaseCertificate = '9021404c27e2554c04d05a9ec70f27ed467e317e13dc64f06a28bab674a9a204'
$signer = 'C:\Program Files (x86)\Android\android-sdk\build-tools\36.0.0\apksigner.bat'
if (!$env:JAVA_HOME) {
    $env:JAVA_HOME = 'C:\Program Files\Microsoft\jdk-17.0.20.8-hotspot'
}

if (!(Test-Path -LiteralPath $keystore -PathType Leaf) -or !(Test-Path -LiteralPath $passwordFile -PathType Leaf)) {
    throw "Falta la clave de distribución o su contraseña en $SigningDirectory. Usa la clave con la que se firmó la APK 2.3.0."
}

$version = & dotnet msbuild $project -nologo -getProperty:ApplicationDisplayVersion
if ($LASTEXITCODE -ne 0 -or $version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'No se pudo leer la versión de la APK.'
}

& dotnet publish $project -c Release -f net10.0-android -p:AndroidPackageFormats=apk -o $publishDirectory -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de Android.' }

$unsigned = Join-Path $publishDirectory 'net.hubnet.hncontrol.apk'
$candidate = Join-Path $publishDirectory 'HN-Control-release.apk'
& $signer sign --ks $keystore --ks-key-alias hncontrol --ks-pass "file:$passwordFile" --out $candidate $unsigned
if ($LASTEXITCODE -ne 0) { throw 'Falló la firma de la APK.' }

$certificate = & $signer verify --print-certs $candidate
if ($LASTEXITCODE -ne 0 -or $certificate -notcontains "Signer #1 certificate SHA-256 digest: $releaseCertificate") {
    throw 'La APK debe llevar el certificado de distribución de HN Control.'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$fileName = "HN-Control-$version.apk"
$output = Join-Path $OutputDirectory $fileName
Copy-Item -LiteralPath $candidate -Destination $output -Force
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
"$hash  $fileName" | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256.txt')
Write-Output "APK lista: $output"
