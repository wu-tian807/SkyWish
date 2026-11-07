[CmdletBinding()]
param([string]$GamePath = $env:CS2_PATH)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $GamePath) { $GamePath = 'D:\SteamLibrary\steamapps\common\Cities Skylines II' }
& (Join-Path $PSScriptRoot 'Build-Mod.ps1') -GamePath $GamePath
$dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnet build (Join-Path $root 'tests/TransportProbe/TransportProbe.csproj') -c Release "-p:GamePath=$GamePath" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Transport probe build failed.' }
& $dotnet build (Join-Path $root 'tests/DeletionProbe/DeletionProbe.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Deletion probe build failed.' }
Push-Location $root
try {
    & npm.cmd test
    if ($LASTEXITCODE -ne 0) { throw 'Automated checks failed.' }
    & npm.cmd run sources:verify
    if ($LASTEXITCODE -ne 0) { throw 'Source reference verification failed.' }
} finally { Pop-Location }
