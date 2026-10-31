$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
$code = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
$sem = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Semantics\ProcessQueryEngine.cs') -Raw -Encoding UTF8

# Checks are separated to show which link is missing instead of an opaque failure.
# The accented E in the RAM heading is assembled as a Unicode code point:
# Windows PowerShell 5.1 must not depend on a script editor's ANSI code page.
$evaluationHeading = 'VALUTAZIONE: ' + [char]0x00C8 + ' UN CONSUMO ECCESSIVO'
$checks = @(
    [PSCustomObject]@{ Name='menu hover in XAML'; Passed=($view -match 'QuickQuestionRoot_MouseEnter') }
    [PSCustomObject]@{ Name='handler menu hover'; Passed=($code -match 'private void QuickQuestionRoot_MouseEnter') }
    [PSCustomObject]@{ Name='voci menu in XAML'; Passed=($view -match 'QuickQuestionMenuItem_Click') }
    [PSCustomObject]@{ Name='handler voci menu'; Passed=($code -match 'private async void QuickQuestionMenuItem_Click') }
    [PSCustomObject]@{ Name='vecchio picker rimosso da XAML'; Passed=($view -notmatch 'QuickQuestionPicker_SelectionChanged') }
    [PSCustomObject]@{ Name='vecchio picker rimosso da C#'; Passed=($code -notmatch 'QuickQuestionPicker_SelectionChanged') }
    [PSCustomObject]@{ Name='riquadro testo in XAML'; Passed=($view -match 'ProcessQueryAnswerText') }
    [PSCustomObject]@{ Name='pannello sezioni in XAML'; Passed=($view -match 'ProcessQuerySectionsPanel') }
    [PSCustomObject]@{ Name='sezioni espandibili in C#'; Passed=(($code -match 'ReportView.Section') -and ((Get-Content (Join-Path $root 'src\ProcessLens.App\ReportView.cs') -Raw -Encoding UTF8) -match 'new Expander')) }
    [PSCustomObject]@{ Name='render rapporto in C#'; Passed=($code -match 'RenderQueryAnswer') }
    [PSCustomObject]@{ Name='sintesi rapporto RAM'; Passed=($sem -match 'DIAGNOSI IN BREVE') }
    [PSCustomObject]@{ Name='mappa memoria virtuale'; Passed=($sem -match 'MAPPA DELLA MEMORIA VIRTUALE') }
    [PSCustomObject]@{ Name='valutazione eccesso RAM'; Passed=($sem.Contains($evaluationHeading)) }
    [PSCustomObject]@{ Name='pressione RAM fisica reale'; Passed=($code -match 'WindowsPhysicalMemoryReader\.Read\(\)') }
)
$failed = @($checks | Where-Object { -not $_.Passed })
if ($failed.Count -gt 0) {
    foreach ($entry in $failed) {
        Write-Host ('FAIL: ' + $entry.Name) -ForegroundColor Red
    }
    throw ('ERRORE: collegamenti del rapporto strutturato mancanti: ' +
        (($failed | ForEach-Object { $_.Name }) -join ', '))
}
Write-Host 'PASS: menu hover WPF, handler, rapporto RAM strutturato e pressione fisica reale.' -ForegroundColor Green
