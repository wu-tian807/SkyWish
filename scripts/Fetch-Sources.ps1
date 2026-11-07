[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$lock = Get-Content -Raw -LiteralPath (Join-Path $root 'references/sources.lock.json') | ConvertFrom-Json
$referenceRoot = [IO.Path]::GetFullPath((Join-Path $root 'third_party'))
New-Item -ItemType Directory -Force -Path $referenceRoot | Out-Null
foreach ($repo in $lock.repositories) {
    if ($repo.repository -notmatch '^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$' -or $repo.commit -notmatch '^[a-f0-9]{40}$') { throw 'Invalid source record.' }
    $target = Join-Path $referenceRoot ($repo.repository.Replace('/', '-'))
    $url = "https://github.com/$($repo.repository).git"
    if (Test-Path -LiteralPath $target) { Write-Output "Keep existing $($repo.repository); run npm run sources:verify to check it."; continue }
    git -c core.hooksPath=NUL init $target
    if ($LASTEXITCODE -ne 0) { throw 'Git init failed.' }
    git -C $target remote add origin $url
    if ($LASTEXITCODE -ne 0) { throw 'Git remote setup failed.' }
    git -C $target -c core.hooksPath=NUL fetch --depth 1 origin $repo.commit
    if ($LASTEXITCODE -ne 0) { throw "Fetch failed: $($repo.repository). Partial directory retained for inspection." }
    git -C $target -c core.hooksPath=NUL checkout --detach FETCH_HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git checkout failed.' }
}
