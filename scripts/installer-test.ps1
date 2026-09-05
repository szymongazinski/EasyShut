$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $repo 'build\release'
if (-not (Test-Path -LiteralPath (Join-Path $out 'EasyShut-Setup.exe'))) { & "$PSScriptRoot\build.ps1" }
& $compiler /nologo /warnaserror+ /utf8output /codepage:65001 /langversion:5 /target:exe "/reference:$out\EasyShut-Setup.exe" "/out:$out\installer-tests.exe" "$repo\tests\InstallerTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer test compilation failed.' }
& "$out\installer-tests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Installer tests failed.' }
