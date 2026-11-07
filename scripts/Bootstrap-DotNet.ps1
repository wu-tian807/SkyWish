[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sdkRoot = Join-Path $root '.tools/dotnet'
$cache = Join-Path $root '.cache/downloads'
if (Test-Path -LiteralPath (Join-Path $sdkRoot 'dotnet.exe')) {
    & (Join-Path $sdkRoot 'dotnet.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'Existing local SDK is not usable.' }
    exit 0
}
New-Item -ItemType Directory -Force -Path $sdkRoot, $cache | Out-Null
$metadataUrl = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$metadata = Invoke-RestMethod -Uri $metadataUrl
$version = $metadata.'latest-sdk'
$release = $metadata.releases | Where-Object { $_.sdk.version -eq $version } | Select-Object -First 1
$archive = $release.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $archive -or -not $archive.hash) { throw 'SDK metadata missing download or SHA512.' }
$uri = [Uri]$archive.url
if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'builds.dotnet.microsoft.com') { throw 'Unexpected SDK download origin.' }
$zip = Join-Path $cache "dotnet-sdk-$version-win-x64.zip"
if (-not (Test-Path -LiteralPath $zip)) {
    Write-Output "Downloading .NET SDK $version into the workspace..."
    Invoke-WebRequest -Uri $uri -OutFile $zip
}
$actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA512).Hash
if ($actual -ne $archive.hash) { throw 'SDK archive SHA512 does not match Microsoft release metadata.' }
Expand-Archive -LiteralPath $zip -DestinationPath $sdkRoot -Force
@{ version = $version; url = $archive.url; sha512 = $actual; metadataUrl = $metadataUrl } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root '.tools/dotnet-sdk.json') -Encoding utf8
& (Join-Path $sdkRoot 'dotnet.exe') --version
if ($LASTEXITCODE -ne 0) { throw 'SDK verification failed.' }
