param([switch]$TestBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Wymagany .NET Framework 4.8 (Windows 10/11).' }
$out = Join-Path $repo $(if ($TestBuild) { 'build\test' } else { 'build\release' })
New-Item -ItemType Directory -Path $out -Force | Out-Null
$common = @('/nologo', '/optimize+', '/warnaserror+', '/utf8output', '/codepage:65001', '/platform:anycpu', '/langversion:5', "/win32manifest:$repo\src\app.manifest")
$common += "/win32icon:$repo\assets\EasyShut.ico"
$sources = @("$repo\src\Core.cs", "$repo\src\Settings.cs", "$repo\src\Ipc.cs", "$repo\src\AssemblyInfo.cs")
if ($TestBuild) { $common += '/define:TESTING' }
& $compiler @common /target:exe "/out:$out\EasyShut.exe" @sources "$repo\src\Cli.cs"
if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji CLI.' }
$gui = @('/target:winexe', "/out:$out\EasyShut-window.exe", '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', "/resource:$repo\assets\EasyShut.ico,EasyShut.App.ico") + $sources + @("$repo\src\Gui.cs", "$repo\src\HelpWindow.cs", "$repo\src\AdvancedWindow.cs", "$repo\src\AppIcon.cs", "$repo\src\AppIdentity.cs")
if ($TestBuild) { $gui += "$repo\tests\TestPlatform.cs" } else { $gui += "$repo\src\NativePower.cs" }
& $compiler @common @gui
if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji GUI.' }
$config = '<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>'
foreach ($name in @('EasyShut.exe.config', 'EasyShut-window.exe.config')) { [IO.File]::WriteAllText((Join-Path $out $name), $config) }
if (-not $TestBuild) {
    $setupFlags = @('/nologo', '/optimize+', '/warnaserror+', '/utf8output', '/codepage:65001', '/platform:anycpu', '/langversion:5', '/target:winexe', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:Microsoft.CSharp.dll', "/win32manifest:$repo\src\app.manifest")
    $setupFlags += @("/win32icon:$repo\assets\EasyShut.ico", "/resource:$repo\assets\EasyShut.ico,EasyShut.App.ico")
    & $compiler @setupFlags /define:UNINSTALL "/out:$out\EasyShut-uninstall.exe" "$repo\src\Setup.cs" "$repo\src\AssemblyInfo.cs" "$repo\src\AppIcon.cs" "$repo\src\AppIdentity.cs" "$repo\src\Shortcuts.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji deinstalatora.' }
    $resources = @()
    foreach ($name in @('EasyShut.exe', 'EasyShut-window.exe', 'EasyShut-uninstall.exe', 'EasyShut.exe.config', 'EasyShut-window.exe.config')) {
        $resources += "/resource:$out\$name,Payload.$name"
    }
    foreach ($name in @('README.md', 'LICENSE')) { $resources += "/resource:$repo\$name,Payload.$name" }
    & $compiler @setupFlags "/out:$out\EasyShut-Setup.exe" @resources "$repo\src\Setup.cs" "$repo\src\AssemblyInfo.cs" "$repo\src\AppIcon.cs" "$repo\src\AppIdentity.cs" "$repo\src\Shortcuts.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji instalatora.' }
}
Write-Output "Zbudowano: $out"
