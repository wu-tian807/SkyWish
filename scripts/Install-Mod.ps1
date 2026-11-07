[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (Get-Process Cities2 -ErrorAction SilentlyContinue) { throw 'Exit Cities Skylines II before updating the mod.' }
$dll = Join-Path $root 'artifacts/mod/CityDirector.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Build the mod first.' }
$cleanup = Join-Path $root 'artifacts/mod/cleanup'
if (-not (Test-Path -LiteralPath (Join-Path $cleanup 'CityDirector.Cleanup.exe'))) { throw 'Build the cleanup worker before installing.' }
$destination = Join-Path ($env:LOCALAPPDATA + 'Low') 'Colossal Order/Cities Skylines II/Mods/CityDirector'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$installed = Join-Path $destination 'CityDirector.dll'
if (Test-Path -LiteralPath $installed) {
    $backupDir = Join-Path $root ('artifacts/mod-backups/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    Copy-Item -LiteralPath $installed -Destination $backupDir
}
Copy-Item -LiteralPath $dll -Destination $installed
Copy-Item -LiteralPath $cleanup -Destination $destination -Recurse -Force
$settings = Join-Path $destination 'CityDirector.settings.json'
if (-not (Test-Path -LiteralPath $settings)) {
    @{ libraryRoot = (Join-Path $root 'asset-library') } | ConvertTo-Json | Set-Content -LiteralPath $settings -Encoding utf8
}
Write-Output "Installed $installed"
Get-Content -LiteralPath $settings
