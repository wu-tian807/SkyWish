[CmdletBinding()]
param([string]$GamePath = $env:CS2_PATH)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $GamePath) {
    $candidates = @($env:CSII_INSTALLATIONPATH, 'D:\SteamLibrary\steamapps\common\Cities Skylines II', 'C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II')
    $GamePath = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'Cities2_Data/Managed/Game.dll')) } | Select-Object -First 1
}
if (-not $GamePath) { throw 'Pass -GamePath or set CS2_PATH to the Cities Skylines II folder.' }
$dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $root '.cache/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $root '.cache/nuget'
& $dotnet build (Join-Path $root 'src/CityDirector.Mod/CityDirector.Mod.csproj') -c Release "-p:GamePath=$GamePath" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
& $dotnet build (Join-Path $root 'src/CityDirector.Cleanup/CityDirector.Cleanup.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Cleanup worker build failed.' }
Write-Output "Built: $root\artifacts\mod\CityDirector.dll (not deployed to the game)"
