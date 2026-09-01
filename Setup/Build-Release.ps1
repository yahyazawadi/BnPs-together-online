# Build and Package Script for BnP Together ONLINE
param(
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = 'Stop'
$Root = "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online"
$PublishDir = "$Root\Publish"
$OutputDir = "$Root\Output"
$ReleaseZip = "$OutputDir\BnPRelay-Release.zip"
$IsccPath = "C:\Users\CLICK\AppData\Local\Programs\Inno Setup 6\ISCC.exe"

Write-Host "=== 1. Terminating any running instances ===" -ForegroundColor Cyan
taskkill /F /IM BnPRelay.exe 2>&1 | Out-Null
Start-Sleep -Milliseconds 500

if (-not $SkipPublish) {
    Write-Host "=== 2. Publishing .NET 8 Single-File Binary ===" -ForegroundColor Cyan
    Remove-Item -Recurse -Force "$PublishDir\*" -ErrorAction SilentlyContinue
    $env:PATH = "$env:PATH;C:\Program Files\dotnet"
    dotnet publish "$Root\BnPRelay\BnPRelay.csproj" -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained true -o "$PublishDir"
}

Write-Host "=== 3. Creating GitHub Release Zip (BnPRelay-Release.zip) ===" -ForegroundColor Cyan
if (Test-Path $ReleaseZip) { Remove-Item -Force $ReleaseZip }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ReleaseZip -CompressionLevel Optimal

Write-Host "=== 4. Compiling Inno Setup Web / Offline Installer ===" -ForegroundColor Cyan
Start-Process -FilePath $IsccPath -ArgumentList "`"$Root\Setup\Installer.iss`"" -Wait -NoNewWindow

Write-Host "=== Build Complete! ===" -ForegroundColor Green
Get-ChildItem $OutputDir | Select-Object Name, Length, LastWriteTime
