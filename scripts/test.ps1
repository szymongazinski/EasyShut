$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $repo 'build\tests'
New-Item -ItemType Directory -Path $out -Force | Out-Null
& $compiler /nologo /warnaserror+ /utf8output /codepage:65001 /langversion:5 /target:exe "/out:$out\core-tests.exe" "$repo\src\Core.cs" "$repo\tests\CoreTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$out\core-tests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
