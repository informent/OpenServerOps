param([Parameter(Mandatory=$true)][string]$Exe)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('OpenServerOps-ui-' + [guid]::NewGuid().ToString('N') + '-b')
$null = New-Item -ItemType Directory -Path (Join-Path $fixture 'a'),(Join-Path $fixture 'b'),(Join-Path $fixture 'addons')
[IO.File]::WriteAllText((Join-Path $fixture 'a/server.log'), 'FIRST_LOG_MARKER')
[IO.File]::WriteAllText((Join-Path $fixture 'b/server.log'), ('historical line' * 150000) + "`nERROR SECOND_LOG_MARKER")
[IO.File]::WriteAllText((Join-Path $fixture 'addons/empty.txt'), '')
[IO.File]::WriteAllText((Join-Path $fixture 'broken.zip'), 'invalid zip')
$zip = [IO.Compression.ZipFile]::Open((Join-Path $fixture 'good.zip'), [IO.Compression.ZipArchiveMode]::Create)
$zip.Dispose()
$before = @{}
Get-ChildItem -LiteralPath $fixture -File -Recurse | ForEach-Object { $before[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash }
$process = $null
function Control($window, $id) {
    $window.FindFirst([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, $id))
}
try {
    $process = Start-Process -FilePath $Exe -ArgumentList @('--folder', ('"' + $fixture + '"')) -PassThru
    $window = $null
    for ($i=0; $i -lt 80 -and $null -eq $window; $i++) {
        Start-Sleep -Milliseconds 250
        $window = [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id))
        if ($process.HasExited) { throw 'Application exited at startup.' }
    }
    if ($null -eq $window) { throw 'Application window not found.' }
    $run = Control $window 'RunChecks'
    $run.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    $state = Control $window 'StateText'
    for ($i=0; $i -lt 120 -and $state.Current.Name -ne 'Checked'; $i++) { Start-Sleep -Milliseconds 250 }
    if ($state.Current.Name -ne 'Checked') { throw ('Scan did not complete: ' + $state.Current.Name) }
    if ((Control $window 'FindingCount').Current.Name -ne '2') { throw 'Expected two findings: empty addon and unreadable ZIP.' }
    if ((Control $window 'StorageResult').Current.Name -notmatch '5 files') { throw 'Inventory count incorrect.' }
    $activity = Control $window 'Activity'
    $list = Control $window 'LogList'
    foreach ($pair in @(@('a\server.log','FIRST_LOG_MARKER'), @('b\server.log','SECOND_LOG_MARKER'))) {
        $item = $list.FindFirst([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty, $pair[0]))
        if ($null -eq $item) { throw ('Missing relative log path: ' + $pair[0]) }
        $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 200
        $text = $activity.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value
        if ($text -notmatch $pair[1]) { throw ('Wrong log preview selected for ' + $pair[0]) }
    }
    $filter = Control $window 'LogFilter'
    $filter.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('b\')
    for ($i=0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 250
        $items = $list.FindAll([Windows.Automation.TreeScope]::Children, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::ListItem))
        if ($items.Count -eq 1 -and $items[0].Current.Name -eq 'b\server.log') { break }
    }
    if ($items.Count -ne 1 -or $items[0].Current.Name -ne 'b\server.log') { throw 'Relative log filtering failed; parent folder names must not match.' }
    foreach ($path in $before.Keys) { if ((Get-FileHash -LiteralPath $path).Hash -ne $before[$path]) { throw 'Audit changed a fixture file.' } }
    Write-Output 'PASS: downloaded/packaged UI scanned the fixture, showed real findings, selected both duplicate-name logs, filtered logs, and preserved all source hashes.'
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -and [IO.Path]::GetFileName($resolved).StartsWith('OpenServerOps-ui-')) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
