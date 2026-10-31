$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$main = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
[xml]$guardian = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\GuardianDashboardView.xaml') -Raw -Encoding UTF8
$code = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
$render = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\GuardianDashboardView.xaml.cs') -Raw -Encoding UTF8
foreach ($name in @('ProcessDetailsSplitter','ToggleDetailsFocusButton','ProcessListPanel','ProcessDetailsScroll',
                    'GuiResourcesExpander','WindowsComponentContextText')) {
    if (-not $main.SelectSingleNode("//*[@*[local-name()='Name']='$name']")) { throw "FAIL R10 R10: missing $name" }
}
foreach ($token in @('_listWidthBeforeFocus = column.Width', 'column.MinWidth = 0;',
    'column.Width = _listWidthBeforeFocus.Value;', 'ProcessListPanel.Visibility = Visibility.Visible;',
    'WindowsComponentContext.Describe(p)', 'ShowWindowsContext_Click', 'ClipboardText.Copy(this, _selfResourceReport)')) {
    if (-not $code.Contains($token)) { throw "FAIL R10 R10: missing integration $token" }
}
foreach ($name in @('DiagnosticDetailsText','DiagnosticOriginalText')) {
    $node = $guardian.SelectSingleNode("//*[@*[local-name()='Name']='$name']")
    if (-not $node -or $node.LocalName -ne 'SelectableText' -or $node.GetAttribute('Text') -notlike '*Mode=OneWay*') {
        throw "FAIL R10 R10: selectable one-way diagnostic output missing: $name"
    }
}
if (-not $render.Contains('value.Diagnostics.Select(GuardianDiagnosticPresentation.From)') -or
    -not $render.Contains('!current.SequenceEqual(diagnostics)') -or
    -not $render.Contains('OriginalMessage == previous?.OriginalMessage')) {
    throw 'FAIL R10 R10: diagnostic projection or unchanged-selection guard missing.'
}
Write-Host 'PASS R10 R10: resizable analysis, focus/return, Windows context and Guardian diagnostic details wired. Source checks only; WPF and DPI checks remain separate.'
