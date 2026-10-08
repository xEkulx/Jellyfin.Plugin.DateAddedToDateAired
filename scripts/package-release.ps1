param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Jellyfin.Plugin.DateAddedToDateAired\Jellyfin.Plugin.DateAddedToDateAired.csproj'
$output = Join-Path $root 'artifacts\release'
$staging = Join-Path $output 'package'
$dllName = 'Jellyfin.Plugin.DateAddedToDateAired.dll'
$zipName = "Jellyfin.Plugin.DateAddedToDateAired_$Version.zip"

Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $staging -Force | Out-Null
dotnet restore $project --configfile (Join-Path $root 'NuGet.config')
dotnet publish $project --configuration Release --no-restore --output $staging "/p:Version=$Version"

$dll = Join-Path $staging $dllName
if (-not (Test-Path -LiteralPath $dll)) { throw "Expected plugin DLL was not produced: $dll" }
Get-ChildItem -LiteralPath $staging -File | Where-Object { $_.Name -ne $dllName } | Remove-Item -Force

$zip = Join-Path $output $zipName
Compress-Archive -LiteralPath $dll -DestinationPath $zip -CompressionLevel Optimal
$sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$zip.sha256" -Value "$sha256  $zipName" -NoNewline
Set-Content -LiteralPath (Join-Path $output 'RELEASE-NOTES.md') -Value @"
# Date Added To Date Aired v$Version

- Jellyfin 10.11.8 / 10.11.9 (`net9.0`)
- Server-side Movie and Episode Date Added mapping from PremiereDate
- No NFO, media-file, client, or direct database changes
"@

Write-Host "Created $zip"
Write-Host "SHA-256: $sha256"
