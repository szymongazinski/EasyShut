$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot\build.ps1" -TestBuild
$testDir = Join-Path $repo 'build\test'
$cli = Join-Path $testDir 'easyshut.exe'
$clock = Join-Path $testDir 'clock.txt'
$log = Join-Path $testDir 'events.txt'
$env:EASYSHUT_TEST_CLOCK = $clock
$env:EASYSHUT_TEST_LOG = $log
[IO.File]::WriteAllText($clock, '0')
[IO.File]::WriteAllText($log, '')
function Set-Clock([double]$seconds) {
    [IO.File]::WriteAllText($clock, $seconds.ToString([Globalization.CultureInfo]::InvariantCulture))
    Start-Sleep -Milliseconds 450
}
function Assert-Contains([string]$text, [string]$expected) {
    if (-not $text.Contains($expected)) { throw "Expected '$expected' in: $text" }
}
function Invoke-Cli([string[]]$Arguments, [int]$ExpectedExit = 0) {
    $text = (& $cli @Arguments 2>&1 | Out-String)
    if ($LASTEXITCODE -ne $ExpectedExit) { throw "Exit $LASTEXITCODE for $Arguments : $text" }
    return $text
}
try {
    Assert-Contains (Invoke-Cli @('-status')) 'Brak aktywnej sesji'
    Assert-Contains (Invoke-Cli @('+1') 1) 'Brak aktywnej sesji'
    Assert-Contains (Invoke-Cli @('3')) '03:00:00'
    Set-Clock 5
    Assert-Contains ([IO.File]::ReadAllText($log)) 'screen-off'
    Set-Clock 9900
    Assert-Contains ([IO.File]::ReadAllText($log)) 'warning:15'
    $hostProcess = Get-Process -Name 'easyshut-window' | Where-Object { $_.Path -eq (Join-Path $testDir 'easyshut-window.exe') }
    if (-not $hostProcess -or $hostProcess.MainWindowHandle -eq 0) { throw 'The background-session warning is not visible.' }
    Set-Clock 10740
    Assert-Contains ([IO.File]::ReadAllText($log)) 'warning:1'
    Assert-Contains (Invoke-Cli @('+1')) '01:01:00'
    Set-Clock 13500
    Set-Clock 14340
    $events = [IO.File]::ReadAllText($log)
    if (($events -split 'warning:15').Length -ne 3 -or ($events -split 'warning:1\r?\n').Length -ne 3) { throw 'Warnings were not re-armed after extension.' }
    Set-Clock 14400
    Assert-Contains ([IO.File]::ReadAllText($log)) 'execute:Shutdown'
    Assert-Contains (Invoke-Cli @('-status')) 'Brak aktywnej sesji'
    Assert-Contains (Invoke-Cli @('0.25', '-sleep', '-screen_on')) '00:15:00'
    Set-Clock 15300
    Assert-Contains ([IO.File]::ReadAllText($log)) 'execute:Sleep'
    Assert-Contains (Invoke-Cli @('-n', '-screen_on')) 'Nigdy'
    Assert-Contains (Invoke-Cli @('+1')) 'bez zmian'
    $beforeInvalid = Invoke-Cli @('-status')
    $null = Invoke-Cli @('1', '-shut', '-sleep') 2
    if ((Invoke-Cli @('-status')) -ne $beforeInvalid) { throw 'Invalid input changed the running session.' }
    Assert-Contains (Invoke-Cli @('1,5', '-screen_on')) '01:30:00'
    Assert-Contains (Invoke-Cli @('6', '-screen_on')) '06:00:00'
    Assert-Contains (Invoke-Cli @('-stop')) 'Brak aktywnej sesji'
    Set-Clock 100000
    if (([IO.File]::ReadAllText($log) -split 'execute:').Length -ne 3) { throw 'Unexpected action after stop.' }
    Write-Output 'PASS CLI/host integration: startup, IPC, deadlines, warnings, extension, replacement, validation, stop.'
} finally {
    & $cli -stop | Out-Null
    # Only terminate the isolated test backend. It contains no native power code.
    Get-Process -Name 'easyshut-window' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $testDir 'easyshut-window.exe') } | Stop-Process
    Remove-Item Env:\EASYSHUT_TEST_CLOCK -ErrorAction SilentlyContinue
    Remove-Item Env:\EASYSHUT_TEST_LOG -ErrorAction SilentlyContinue
}
