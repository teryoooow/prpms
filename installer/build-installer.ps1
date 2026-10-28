# Builds the self-contained publish + Inno Setup installer for PRPMS.
# Output: installer\Output\PRPMS-Setup-<version>.exe
#
# Usage:  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
#
# Note: dotnet publish is routed through %TEMP% because MSBuild's publish
# copy target mishandles apostrophes in paths (this repo lives under
# "Eden's Booking").

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot   # repo root
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) {
    $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
}
if (-not (Test-Path $iscc)) { throw 'Inno Setup 6 not found (ISCC.exe).' }

# 1. Self-contained publish (no .NET prerequisite on target machines)
$tmp = Join-Path $env:TEMP 'opencode\rs-publish'
Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
& $dotnet publish (Join-Path $root 'src\ResortSync\ResortSync.csproj') `
    -c Release -r win-x64 --self-contained true -o $tmp -v minimal
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $tmp 'PRPMS.exe'))) {
    throw 'dotnet publish failed.'
}
$pub = Join-Path $root 'publish'
Remove-Item -Recurse -Force $pub -ErrorAction SilentlyContinue
Copy-Item -Recurse $tmp $pub
Write-Host "publish: $((Get-ChildItem $pub -Recurse -File | Measure-Object).Count) files"

# 2. Compile installer
& $iscc (Join-Path $root 'installer\ResortSync.iss')
if ($LASTEXITCODE -ne 0) { throw 'ISCC compile failed.' }

Get-ChildItem (Join-Path $root 'installer\Output') -Filter '*.exe' |
    Select-Object FullName, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) }}
