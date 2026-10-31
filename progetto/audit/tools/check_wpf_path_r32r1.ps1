$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$tray=Get-Content (Join-Path $root 'src/ProcessLens.App/TrayWindowController.cs') -Raw -Encoding UTF8
if(-not $tray.Contains('IconFile.ReadEmbedded') -or $tray.Contains('System.IO.Path.Combine(AppContext.BaseDirectory')){throw 'FAIL R32 R2: tray icon must use embedded bytes instead of an external path.'}
$usingCheck=Get-Content (Join-Path $root 'audit/tools/typecheck_wpf_sources_r32r1.py') -Raw -Encoding UTF8
if(-not $usingCheck.Contains('msbuild-wpf-usings.json') -or -not $usingCheck.Contains('wpf_globals if name')){throw 'FAIL R32 R1: C# checks must use evaluated WPF implicit usings.'}
Write-Host 'PASS R32 R1: embedded icon and evaluated WPF implicit usings; markup and Windows execution remain separate.'
