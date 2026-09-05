$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$running = Get-Process -Name 'easyshut-window', 'easyshut' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and [IO.Path]::GetDirectoryName($_.Path) -eq $target }
if ($running) { throw 'Zamknij easyshut przed odinstalowaniem (w tle: easyshut -stop, następnie poczekaj 11 sekund).' }
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$parts = @($userPath -split ';' | Where-Object { $_ -and $_.TrimEnd('\') -ne $target })
[Environment]::SetEnvironmentVariable('Path', ($parts -join ';'), 'User')
$env:Path = (($env:Path -split ';' | Where-Object { $_ -and $_.TrimEnd('\') -ne $target }) -join ';')
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'easyshut.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcutPath).TargetPath -eq (Join-Path $target 'easyshut-window.exe')) { Remove-Item -LiteralPath $shortcutPath }
}
# Remove only known package files in this exact directory; never recurse.
foreach ($name in @('easyshut.exe', 'easyshut-window.exe', 'easyshut.exe.config', 'easyshut-window.exe.config', 'install.ps1', 'README.md', 'LICENSE', 'uninstall.ps1')) {
    $item = [IO.Path]::GetFullPath((Join-Path $target $name))
    if ([IO.Path]::GetDirectoryName($item) -ne $target) { throw 'Nieprawidłowy katalog odinstalowania.' }
    if (Test-Path -LiteralPath $item) { Remove-Item -LiteralPath $item }
}
Write-Output 'Odinstalowano easyshut. Otwórz nowe okno terminala, aby odświeżyć PATH.'
