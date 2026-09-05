param([switch]$TestBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Wymagany .NET Framework 4.8 (Windows 10/11).' }
$out = Join-Path $repo $(if ($TestBuild) { 'build\test' } else { 'build\release' })
New-Item -ItemType Directory -Path $out -Force | Out-Null
$common = @('/nologo', '/optimize+', '/warnaserror+', '/utf8output', '/codepage:65001', '/platform:anycpu', '/langversion:5', "/win32manifest:$repo\src\app.manifest")
$sources = @("$repo\src\Core.cs", "$repo\src\Ipc.cs", "$repo\src\AssemblyInfo.cs")
if ($TestBuild) { $common += '/define:TESTING' }
& $compiler @common /target:exe "/out:$out\easyshut.exe" @sources "$repo\src\Cli.cs"
if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji CLI.' }
$gui = @('/target:winexe', "/out:$out\easyshut-window.exe", '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll') + $sources + @("$repo\src\Gui.cs")
if ($TestBuild) { $gui += "$repo\tests\TestPlatform.cs" } else { $gui += "$repo\src\NativePower.cs" }
& $compiler @common @gui
if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji GUI.' }
$config = '<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>'
foreach ($name in @('easyshut.exe.config', 'easyshut-window.exe.config')) { [IO.File]::WriteAllText((Join-Path $out $name), $config) }
Write-Output "Zbudowano: $out"
