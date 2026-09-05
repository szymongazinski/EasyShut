$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $repo 'build\tests'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$before = powercfg /query SCHEME_CURRENT SUB_SLEEP
& $compiler /nologo /warnaserror+ /utf8output /codepage:65001 /langversion:5 /target:exe /reference:System.Windows.Forms.dll "/out:$out\native-smoke.exe" "$repo\src\Core.cs" "$repo\src\NativePower.cs" "$repo\tests\NativeSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Native smoke test compilation failed.' }
& "$out\native-smoke.exe"
if ($LASTEXITCODE -ne 0) { throw 'Native smoke test failed.' }
$after = powercfg /query SCHEME_CURRENT SUB_SLEEP
if (Compare-Object $before $after) { throw 'Power plan settings changed.' }
Write-Output 'PASS power plan sleep settings unchanged.'
