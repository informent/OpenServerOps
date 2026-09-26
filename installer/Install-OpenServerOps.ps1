param([string]$InstallRoot = "$env:LOCALAPPDATA\OpenServerOps")
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
Copy-Item (Join-Path $source 'OpenServerOps.exe') $InstallRoot -Force
Get-ChildItem $source -File | Where-Object Name -ne 'OpenServerOps.exe' | Copy-Item -Destination $InstallRoot -Force
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'OpenServerOps.lnk'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut); $link.TargetPath = Join-Path $InstallRoot 'OpenServerOps.exe'; $link.WorkingDirectory = $InstallRoot; $link.Description = 'OpenServerOps local server console'; $link.Save()
Write-Host "Installed OpenServerOps to $InstallRoot"
