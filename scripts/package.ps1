$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot\build.ps1"
$dist = Join-Path $repo 'dist'
$stage = Join-Path $dist 'easyshut-1.0.0-windows'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($name in @('easyshut.exe', 'easyshut-window.exe', 'easyshut.exe.config', 'easyshut-window.exe.config')) { Copy-Item -LiteralPath (Join-Path $repo "build\release\$name") -Destination $stage -Force }
foreach ($name in @('install.ps1', 'uninstall.ps1', 'README.md', 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $stage -Force }
$zip = Join-Path $dist 'easyshut-1.0.0-windows.zip'
Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), "$hash  easyshut-1.0.0-windows.zip`n", (New-Object Text.UTF8Encoding $false))
Write-Output "Gotowy pakiet: $zip"
