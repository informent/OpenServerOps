param([string]$InstallRoot = "$env:LOCALAPPDATA\OpenServerOps")
$ErrorActionPreference = 'Stop'
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'OpenServerOps.lnk'
if (Test-Path $shortcut) { Remove-Item $shortcut -Force }
if (Test-Path $InstallRoot) { Remove-Item $InstallRoot -Recurse -Force }
Write-Host 'OpenServerOps removed.'
