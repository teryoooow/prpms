# Builds a signed Release APK for the PRPMS Android app.
# Output: installer\Output\com.terelle.prpms-Signed.apk
#         (also left at src\ResortSync.Mobile\bin\Release\net8.0-android\)
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File installer\build-apk.ps1
#
# Signing secrets live in installer\keystore\keystore.local.env (gitignored).
# Generate a keystore once with:
#   keytool -genkeypair -v -keystore installer\keystore\prpms.keystore ^
#     -alias prpms -keyalg RSA -keysize 2048 -validity 10000 ^
#     -storepass <password> -keypass <password> ^
#     -dname "CN=PRPMS, OU=ResortSync, O=Eden Resort, L=Manila, C=PH"

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot   # repo root
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

# Load signing config
$envFile = Join-Path $PSScriptRoot 'keystore\keystore.local.env'
if (-not (Test-Path $envFile)) {
    throw "Missing $envFile - create a keystore and write KEYSTORE_PATH/ALIAS/PASSWORD lines (see header comment)."
}
$secrets = @{}
Get-Content $envFile | ForEach-Object {
    if ($_ -match '^\s*([A-Z_]+)\s*=\s*(.+?)\s*$') {
        $secrets[$Matches[1]] = $Matches[2]
    }
}
foreach ($k in 'KEYSTORE_PATH', 'KEYSTORE_ALIAS', 'KEYSTORE_PASSWORD') {
    if (-not $secrets.ContainsKey($k) -or [string]::IsNullOrWhiteSpace($secrets[$k])) {
        throw "keystore.local.env is missing $k"
    }
}
$ksPath = $secrets['KEYSTORE_PATH']
if (-not [System.IO.Path]::IsPathRooted($ksPath)) {
    $ksPath = Join-Path $root $ksPath
}
if (-not (Test-Path $ksPath)) { throw "Keystore not found: $ksPath" }

# Build + sign (property names match Xamarin.Android.Common.targets:
#   AndroidKeyStore / AndroidSigningKeyStore / AndroidSigningStorePass /
#   AndroidSigningKeyAlias / AndroidSigningKeyPass)
$proj = Join-Path $root 'src\ResortSync.Mobile\ResortSync.Mobile.csproj'
$alias = $secrets['KEYSTORE_ALIAS']
$storePass = $secrets['KEYSTORE_PASSWORD']
$keyPass = $secrets['KEYSTORE_PASSWORD']
Write-Host "Building signed Release APK..."
& $dotnet build $proj -f net8.0-android -c Release `
    "-p:AndroidKeyStore=true" `
    "-p:AndroidSigningKeyStore=$ksPath" `
    "-p:AndroidSigningKeyAlias=$alias" `
    "-p:AndroidSigningStorePass=$storePass" `
    "-p:AndroidSigningKeyPass=$keyPass" `
    --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

$apk = Join-Path $root 'src\ResortSync.Mobile\bin\Release\net8.0-android\com.terelle.prpms-Signed.apk'
if (-not (Test-Path $apk)) { throw "Expected APK not found: $apk" }

# Verify signature
$apksigner = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Android\Sdk\build-tools') -Filter apksigner.bat -Recurse -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if ($apksigner) {
    Write-Host "Verifying signature..."
    & $apksigner.FullName verify --print-certs $apk
    if ($LASTEXITCODE -ne 0) { throw 'apksigner verify failed.' }
} else {
    Write-Warning 'apksigner.bat not found - skipped signature verification.'
}

$fi = Get-Item $apk
Write-Host ("APK ready: {0} ({1:N1} MB)" -f $fi.FullName, ($fi.Length / 1MB))

# Copy next to the desktop installer (installer\Output)
$outDir = Join-Path $root 'installer\Output'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$dest = Join-Path $outDir 'com.terelle.prpms-Signed.apk'
Copy-Item -Path $apk -Destination $dest -Force
$fi2 = Get-Item $dest
Write-Host ("Copied: {0} ({1:N1} MB)" -f $fi2.FullName, ($fi2.Length / 1MB))
