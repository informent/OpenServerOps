$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$xaml = Get-Content (Join-Path $root 'MainWindow.xaml') -Raw
foreach ($name in @('ServerName','ServerPath','StateText','LastCheck','FindingCount','Activity','HealthResult','StorageResult','AddonResult','BackupResult','LogFilter','LogList','DarkMode')) { if ($xaml -notmatch ('x:Name="' + $name + '"')) { throw "Missing required UI control: $name" } }
foreach ($script in @('installer/Install-OpenServerOps.ps1','installer/Uninstall-OpenServerOps.ps1','tools/Sign-Release.ps1')) { if (!(Test-Path (Join-Path $root $script))) { throw "Missing distribution script: $script" } }
if (!(Test-Path (Join-Path $root 'installer/README.md'))) { throw 'Missing installer documentation.' }
foreach ($contract in @('AutomationProperties.SetName(DarkMode','AutomationProperties.SetName(LogFilter','AutomationProperties.SetName(LogList','AutomationProperties.SetName(Activity')) { if ((Get-Content (Join-Path $root 'MainWindow.xaml.cs') -Raw) -notlike "*$contract*") { throw "Missing accessibility contract: $contract" } }
Write-Host 'PASS: UI control, accessibility, installer, and signing gates'
