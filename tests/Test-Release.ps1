$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root ('test-output/release-' + [guid]::NewGuid().ToString('N'))
dotnet publish (Join-Path $root 'OpenServerOps.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publishing failed.' }
$exe = Join-Path $out 'OpenServerOps.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Published executable was not produced.' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'PackageUi.ps1') -Exe $exe
if ($LASTEXITCODE -ne 0) { throw 'Packaged application workflow failed.' }
& (Join-Path $root 'tools/Verify-Release.ps1') -Executable $exe
Write-Host 'PASS: self-contained publish and packaged workflow'
