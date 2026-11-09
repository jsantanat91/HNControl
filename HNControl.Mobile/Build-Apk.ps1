param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\entrega')
)
$ErrorActionPreference = 'Stop'
$buildRoot = Join-Path $env:LOCALAPPDATA 'HNControlBuild'
$signingRoot = Join-Path $env:LOCALAPPDATA 'HNControlSigning'
$javaRoot = 'C:\Program Files\Microsoft\jdk-17.0.20.8-hotspot'
$androidTools = 'C:\Program Files (x86)\Android\android-sdk\build-tools\36.0.0'
$env:JAVA_HOME = $javaRoot
New-Item -ItemType Directory -Force $buildRoot, $signingRoot, $OutputDirectory | Out-Null
$passwordFile = Join-Path $signingRoot 'keystore-password.txt'
$keystore = Join-Path $signingRoot 'hncontrol-release.jks'
if (!(Test-Path -LiteralPath $keystore)) {
    if (Test-Path -LiteralPath $passwordFile) {
        throw "Existe un archivo de contraseña sin clave. Revisa $signingRoot antes de continuar."
    }
    $bytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    [IO.File]::WriteAllText($passwordFile, [Convert]::ToBase64String($bytes))
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls.exe $signingRoot /inheritance:r /grant:r "$($identity):(OI)(CI)F" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo proteger la carpeta de firma.' }
    & "$javaRoot\bin\keytool.exe" -genkeypair -keystore $keystore -storetype JKS -alias hncontrol -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=HN Control, O=Hubnet Solutions, C=MX' "-storepass:file" $passwordFile "-keypass:file" $passwordFile
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la clave de firma.' }
}
if (!(Test-Path -LiteralPath $passwordFile)) { throw 'Falta la contraseña de la clave de firma.' }
& dotnet publish (Join-Path $PSScriptRoot 'HNControl.Mobile.csproj') -c Release -f net10.0-android "-p:BaseIntermediateOutputPath=$buildRoot\obj\" "-p:BaseOutputPath=$buildRoot\bin\" -p:AndroidPackageFormats=apk -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de Android.' }
$unsigned = Join-Path $buildRoot 'bin\Release\net10.0-android\publish\net.hubnet.hncontrol.apk'
$output = Join-Path $OutputDirectory 'HN-Control-2.2.0.apk'
& "$androidTools\apksigner.bat" sign --ks $keystore --ks-key-alias hncontrol --ks-pass "file:$passwordFile" --out $output $unsigned
if ($LASTEXITCODE -ne 0) { throw 'Falló la firma de la APK.' }
& "$androidTools\apksigner.bat" verify $output
if ($LASTEXITCODE -ne 0) { throw 'La firma de la APK no es válida.' }
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
"$hash  HN-Control-2.2.0.apk" | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256.txt')
Write-Output "APK lista: $output"
