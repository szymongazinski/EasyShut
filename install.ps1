param([string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\easyshut'))
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$target = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
$files = @('easyshut.exe', 'easyshut-window.exe', 'easyshut.exe.config', 'easyshut-window.exe.config', 'install.ps1', 'uninstall.ps1', 'README.md', 'LICENSE')
foreach ($name in @('easyshut.exe', 'easyshut-window.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name))) { throw 'Wypakuj cały pakiet wydania przed instalacją.' }
}
$running = Get-Process -Name 'easyshut-window', 'easyshut' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and [IO.Path]::GetDirectoryName($_.Path) -eq $target }
if ($running) { throw 'Zamknij easyshut przed aktualizacją (w tle: easyshut -stop, następnie poczekaj 11 sekund).' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
if ([IO.Path]::GetFullPath($source).TrimEnd('\') -ne $target) {
    foreach ($name in $files) {
        $item = Join-Path $source $name
        if (Test-Path -LiteralPath $item) { Copy-Item -LiteralPath $item -Destination (Join-Path $target $name) -Force }
    }
}
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$parts = @($userPath -split ';' | Where-Object { $_ -and $_.TrimEnd('\') -ne $target })
[Environment]::SetEnvironmentVariable('Path', (($parts + $target) -join ';'), 'User')
if (-not (($env:Path -split ';') | Where-Object { $_.TrimEnd('\') -eq $target })) { $env:Path += ';' + $target }
$startMenu = [Environment]::GetFolderPath('Programs')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenu 'easyshut.lnk'))
$shortcut.TargetPath = Join-Path $target 'easyshut-window.exe'
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'easyshut — blokada usypiania i odliczanie'
$shortcut.Save()
if (-not ('EasyShutInstall.EnvironmentNotice' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; namespace EasyShutInstall { public static class EnvironmentNotice { [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r); } }'
}
$result = [UIntPtr]::Zero
[void][EasyShutInstall.EnvironmentNotice]::SendMessageTimeout([IntPtr]0xffff, 0x1a, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$result)
Write-Output "Zainstalowano easyshut: $target"
Write-Output 'Otwórz nowe okno terminala i wpisz easyshut lub easyshut -help. Jest też skrót w menu Start.'
