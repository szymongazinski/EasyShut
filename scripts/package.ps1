$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot\build.ps1"
$dist = Join-Path $repo 'dist'
$stage = Join-Path $dist 'EasyShut-1.2.1-windows'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($name in @('EasyShut.exe', 'EasyShut-window.exe', 'EasyShut.exe.config', 'EasyShut-window.exe.config')) { Copy-Item -LiteralPath (Join-Path $repo "build\release\$name") -Destination $stage -Force }
foreach ($name in @('README.md', 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $stage -Force }
$zip = Join-Path $dist 'EasyShut-1.2.1-windows.zip'
Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force
Copy-Item -LiteralPath (Join-Path $repo 'build\release\EasyShut-Setup.exe') -Destination (Join-Path $dist 'EasyShut-Setup.exe') -Force
$hashes = foreach ($name in @('EasyShut-1.2.1-windows.zip', 'EasyShut-Setup.exe')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $dist $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), (($hashes -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
Write-Output "Gotowy pakiet: $zip"
