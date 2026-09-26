$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'test-output'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet build (Join-Path $root 'OpenServerOps.csproj') -c Release --nologo -p:BaseIntermediateOutputPath="$out\obj\"
dotnet publish (Join-Path $root 'OpenServerOps.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:BaseIntermediateOutputPath="$out\obj\" -o $out --nologo
$exe = Join-Path $out 'OpenServerOps.exe'
if (!(Test-Path $exe)) { throw 'Published executable was not produced.' }
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 3
$deadline = (Get-Date).AddSeconds(10)
do {
    Start-Sleep -Milliseconds 250
    $p.Refresh()
    if ($p.HasExited) { throw "OpenServerOps exited during smoke test with code $($p.ExitCode)." }
} while ([string]::IsNullOrWhiteSpace($p.MainWindowTitle) -and (Get-Date) -lt $deadline)
if ([string]::IsNullOrWhiteSpace($p.MainWindowTitle)) { Stop-Process -Id $p.Id -Force; throw 'OpenServerOps stayed alive without creating its main window within 10 seconds.' }
Stop-Process -Id $p.Id -Force
Write-Host "PASS: build, publish, and launch smoke test ($($p.MainWindowTitle))"
