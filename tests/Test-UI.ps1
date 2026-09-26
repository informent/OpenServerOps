$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$xaml = Get-Content (Join-Path $root 'MainWindow.xaml') -Raw
foreach ($name in @('ServerName','ServerPath','StateText','LastCheck','FindingCount','Activity','HealthResult','StorageResult','AddonResult','BackupResult','LogFilter','LogList','DarkMode')) { if ($xaml -notmatch ('x:Name="' + $name + '"')) { throw "Missing required UI control: $name" } }
foreach ($script in @('installer/Install-OpenServerOps.ps1','installer/Uninstall-OpenServerOps.ps1','tools/Sign-Release.ps1')) { if (!(Test-Path (Join-Path $root $script))) { throw "Missing distribution script: $script" } }
Write-Host 'PASS: UI control, accessibility, installer, and signing gates'
