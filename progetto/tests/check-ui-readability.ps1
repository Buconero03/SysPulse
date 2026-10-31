$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml'
[xml]$xaml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
$content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
$checks = @(
    @('window centered at startup', 'WindowStartupLocation="CenterScreen"'),
    @('filters and counters on separate rows', '<WrapPanel Margin="0,11,0,0"'),
    @('text wraps in process explanations', 'x:Name="ListExplanationText"'),
    @('process labels wrap', '<WrapPanel Margin="0,4,0,0">'),
    @('technical identification section', 'ELEMENTI DI IDENTIFICAZIONE')
)
foreach ($check in $checks) {
    if (-not $content.Contains($check[1])) { throw ('FAIL UI readability: ' + $check[0]) }
    Write-Host ('PASS UI readability: ' + $check[0]) -ForegroundColor Green
}
# duplicate x:Name attributes would otherwise fail when WPF compiles.
$allNamed = [regex]::Matches($content, 'x:Name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$dupes = $allNamed | Group-Object | Where-Object { $_.Count -gt 1 }
if ($dupes) { throw ('FAIL UI duplicate x:Name: ' + (($dupes | ForEach-Object { $_.Name }) -join ', ')) }
Write-Host 'PASS UI readability: XML valido e nomi univoci' -ForegroundColor Green

# v0.19.16 R3: prevent long metric labels from spilling into adjacent columns.
$rowSource = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\ProcessRow.cs') -Raw -Encoding UTF8
$windowSource = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
if (-not $content.Contains('Text="{Binding MemoryShortLabel}"') -or
    -not $content.Contains('ToolTip="{Binding MemoryDescription}"') -or
    -not $rowSource.Contains('CompactMemoryLabel(ProcessSnapshot process)')) {
    throw 'FAIL UI readability: compact memory label with full metric explanation missing.'
}
if (-not $content.Contains('x:Name="DetailMemoryLabel" Text="WS PRIVATO"') -or
    -not $windowSource.Contains('DetailMemoryLabel.ToolTip = p.MemoryDescription')) {
    throw 'FAIL UI readability: memory metric details are not labelled accessibly.'
}
if (-not $content.Contains('TextWrapping="Wrap" MinHeight="27"') -or
    $content.Contains('Text="{Binding MemoryDisplayLabel}" FontSize="10"')) {
    throw 'FAIL UI readability: labels risk overlapping adjacent metric columns.'
}
Write-Host 'PASS UI readability v0.19.16 R3: metric columns and technical tooltips.' -ForegroundColor Green

# v0.19.16 R4: optional in-place memory guide; existing counters are unchanged.
foreach ($name in @('DetailMemoryGuideExpander', 'DetailMemoryGuideExampleText')) {
    if (-not $content.Contains(('x:Name="' + $name + '"'))) {
        throw ('FAIL UI readability: missing RAM guide element ' + $name)
    }
}
if (-not $content.Contains('IsExpanded="False"') -or
    -not $content.Contains('PrivateWorkingSetSize, WorkingSetSize e PrivateUsage') -or
    -not $windowSource.Contains('ProcessMemoryGuide.DescribeCurrentValues(p)')) {
    throw 'FAIL UI readability: expanded RAM documentation must show source and live PID counters.'
}
Write-Host 'PASS UI readability R4: optional RAM guide, Windows API source and PID example.' -ForegroundColor Green
