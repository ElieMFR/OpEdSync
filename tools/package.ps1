# Builds the plugin and produces the release zip: dist/opedsync_<version>.zip (the DLL at the root), then prints its MD5
# for the "checksum" field of manifest.json.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version

dotnet build (Join-Path $root 'Jellyfin.Plugin.OpEdSync') -c Release
$dll = Join-Path $root 'Jellyfin.Plugin.OpEdSync/bin/Release/net10.0/Jellyfin.Plugin.OpEdSync.dll'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "opedsync_$version.zip"
Compress-Archive -Path $dll -DestinationPath $zip -Force

Write-Host "Zip : $zip"
Write-Host "MD5 : $((Get-FileHash $zip -Algorithm MD5).Hash.ToLowerInvariant())"
