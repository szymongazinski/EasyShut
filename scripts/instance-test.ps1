$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot\build.ps1" -TestBuild
$first = Join-Path $repo 'build\test'
$second = Join-Path $repo 'build\instance-copy'
New-Item -ItemType Directory -Path $second -Force | Out-Null
foreach ($name in @('EasyShut.exe', 'EasyShut-window.exe', 'EasyShut.exe.config', 'EasyShut-window.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $first $name) -Destination (Join-Path $second $name) -Force
}
$hosts = @((Join-Path $first 'EasyShut-window.exe'), (Join-Path $second 'EasyShut-window.exe'))
$env:EASYSHUT_TEST_SETTINGS = Join-Path $first 'instance-settings.xml'
if (Test-Path -LiteralPath $env:EASYSHUT_TEST_SETTINGS) { Remove-Item -LiteralPath $env:EASYSHUT_TEST_SETTINGS }
function Get-TestHosts { @(Get-Process -Name 'EasyShut-window' -ErrorAction SilentlyContinue | Where-Object { $hosts -contains $_.Path }) }
function Wait-OneHost {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 150
        $running = Get-TestHosts
        if ($running.Count -eq 1) { return $running[0].Id }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Expected exactly one host, got $($running.Count)"
}
function Assert-State([string]$text, [string]$expected) { if (-not $text.Contains($expected)) { throw "Missing '$expected' in $text" } }
if ((Get-TestHosts).Count -ne 0) { throw 'Close the isolated test build before running the instance test.' }
try {
    # Deliberately race startup from two directories. No power actions or visible windows.
    $launches = @(0..7 | ForEach-Object { Start-Process -FilePath $hosts[$_ % 2] -ArgumentList '--background' -WindowStyle Hidden -PassThru })
    $null = Wait-OneHost
    $output = (& (Join-Path $second 'EasyShut.exe') -n -screen_on | Out-String)
    Assert-State $output 'Nigdy'
    $pidBefore = Wait-OneHost
    foreach ($folder in @($first, $second, $first, $second)) {
        & (Join-Path $folder 'EasyShut.exe') -n -screen_on | Out-Null
    }
    if ((Wait-OneHost) -ne $pidBefore) { throw 'A repeated launch replaced the existing host.' }
    Write-Output 'PASS concurrent startup from two folders: one shared host and unchanged PID.'
    [IO.File]::WriteAllText($env:EASYSHUT_TEST_SETTINGS, '<AdvancedSettings><Version>1</Version><ProtectDocuments>true</ProtectDocuments><Warnings><WarningRule><BeforeMinutes>5</BeforeMinutes><MinimumSessionHours>0</MinimumSessionHours></WarningRule></Warnings></AdvancedSettings>')
    $output = (& (Join-Path $first 'EasyShut.exe') -n -screen_on | Out-String)
    Assert-State $output 'Ochrona dokumentów: włączona'
    Assert-State $output 'Ostrzeżenia: 1'
    & (Join-Path $first 'EasyShut.exe') -stop | Out-Null
    Get-TestHosts | Stop-Process
    $output = (& (Join-Path $second 'EasyShut.exe') -n -screen_on | Out-String)
    Assert-State $output 'Ochrona dokumentów: włączona'
    Assert-State $output 'Ostrzeżenia: 1'
    Write-Output 'PASS saved protection and custom warnings survive a host restart and another executable location.'
} finally {
    & (Join-Path $first 'EasyShut.exe') -stop | Out-Null
    Get-TestHosts | Stop-Process
    Remove-Item Env:\EASYSHUT_TEST_SETTINGS -ErrorAction SilentlyContinue
}
