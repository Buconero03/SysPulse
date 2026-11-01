$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$check = Join-Path $root 'tests/check-localized-window-r35r2.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('processlens-r35r2-' + [Guid]::NewGuid().ToString('N'))
try {
    $app = Join-Path $temporary 'src/ProcessLens.App'
    New-Item -ItemType Directory -Path (Join-Path $app 'Localization'),(Join-Path $app 'Localized') -Force | Out-Null
    $controls = Join-Path $app 'Localization/LocalizedControls.cs'
    $code = Join-Path $app 'Localized/MainWindow.xaml.g.cs'
    Set-Content -LiteralPath (Join-Path $app 'Localized/MainWindow.g.xaml') -Encoding UTF8 -Value '<l:LocalizedWindow xmlns:l="clr-namespace:ProcessLens.App" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="ProcessLens.App.MainWindow" />'
    Set-Content -LiteralPath $controls -Value 'public sealed class LocalizedWindow : Window {}'
    Set-Content -LiteralPath $code -Value 'public partial class MainWindow : global::ProcessLens.App.LocalizedWindow {}'
    $rejected = $false
    try { & $check -ProjectRoot $temporary } catch { $rejected = $_.Exception.Message.Contains('inheritable') }
    if (-not $rejected) { throw 'FAIL R35 R2: sealed XAML root was accepted.' }
    Write-Host 'PASS R35 R2 fixture: sealed XAML root rejected before compilation (CS0509 regression).'
    Set-Content -LiteralPath $controls -Value 'public class LocalizedWindow : Window {}'
    Set-Content -LiteralPath $code -Value 'public partial class MainWindow : Window {}'
    $rejected = $false
    try { & $check -ProjectRoot $temporary } catch { $rejected = $_.Exception.Message.Contains('same base') }
    if (-not $rejected) { throw 'FAIL R35 R2: mismatched partial base was accepted.' }
    Write-Host 'PASS R35 R2 fixture: mismatched partial base rejected (CS0263 regression).'
    Set-Content -LiteralPath $code -Value 'public partial class MainWindow : global::ProcessLens.App.LocalizedWindow {}'
    & $check -ProjectRoot $temporary
    Write-Host 'PASS R35 R2 fixture: inheritable matching root accepted; synthetic files only, no WPF window.'
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
