$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
$code = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
$x = 'http://schemas.microsoft.com/winfx/2006/xaml'
$style = $view.SelectSingleNode("//*[local-name()='Style' and @*[local-name()='Key']='ActionButtonStyle']")
if (-not $style.SelectSingleNode(".//*[local-name()='TextBlock' and @TextWrapping='Wrap' and @LineHeight='20']")) {
    throw 'FAIL R9: native action buttons need wrapped text and line spacing.'
}
if (-not $style.SelectSingleNode("./*[local-name()='Setter' and @Property='MinHeight' and @Value='36']")) {
    throw 'FAIL R9: action buttons need a minimum height.'
}
function Get-ButtonLayoutViolations([xml]$document) {
    foreach ($button in $document.SelectNodes("//*[local-name()='Button']")) {
        if ($button.GetAttribute('Style') -notin @('{StaticResource FilterButtonStyle}', '{StaticResource ActionButtonStyle}') -or
            $button.HasAttribute('Height')) {
            [PSCustomObject]@{
                Name = $button.GetAttribute('Name', 'http://schemas.microsoft.com/winfx/2006/xaml')
                Content = $button.GetAttribute('Content')
                Style = $button.GetAttribute('Style')
                Height = $button.GetAttribute('Height')
            }
        }
    }
}
$violations = @(Get-ButtonLayoutViolations $view)
if ($violations.Count -gt 0) {
    $details = ($violations | ForEach-Object {
        "Name='$($_.Name)' Content='$($_.Content)' Style='$($_.Style)' Height='$($_.Height)'"
    }) -join '; '
    throw ('FAIL R9: unstyled or fixed-height button: ' + $details)
}
# R10 R1: exercise the actual guard with deliberately broken copies of the markup.
foreach ($mutation in @('MissingStyle', 'UnknownStyle', 'FixedHeight')) {
    [xml]$probe = $view.OuterXml
    $button = $probe.SelectSingleNode("//*[@*[local-name()='Name']='AnalyzeRamDirectButton']")
    if ($null -eq $button) { throw 'FAIL R10 R1: direct RAM action missing.' }
    switch ($mutation) {
        'MissingStyle' { $button.RemoveAttribute('Style') }
        'UnknownStyle' { $button.SetAttribute('Style', '{StaticResource MissingStyle}') }
        'FixedHeight' { $button.SetAttribute('Height', '20') }
    }
    $rejected = @(Get-ButtonLayoutViolations $probe)
    if ($rejected.Count -ne 1 -or $rejected[0].Name -ne 'AnalyzeRamDirectButton') {
        throw ('FAIL R10 R1: button layout guard missed mutation ' + $mutation)
    }
}
Write-Host 'PASS R10 R1: all buttons styled; missing/unknown styles and fixed-height regressions rejected.'
foreach ($name in @('DetailCpuAverageText','DetailCpuPeakText','DetailCpuText','DetailMemoryText',
    'DetailPrivateWorkingSetText','DetailWorkingSetText','DetailPrivateCommitText','CpuGraphScaleText')) {
    $node = $view.SelectSingleNode("//*[@*[local-name()='Name']='$name']")
    if ($node.GetAttribute('TextWrapping') -ne 'Wrap') { throw ('FAIL R9: metric cannot wrap: ' + $name) }
}
$network = $view.SelectSingleNode("//*[@*[local-name()='Name']='DetailNetworkStateText']")
if (($network.LocalName -eq 'TextBlock' -and $network.GetAttribute('LineHeight') -ne '21') -or $network.GetAttribute('TextWrapping') -ne 'Wrap') {
    throw 'FAIL R9: network text lacks line spacing or wrapping.'
}
$quick = $view.SelectSingleNode("//*[@*[local-name()='Name']='QuickQuestionRoot']")
if (-not $quick.SelectSingleNode("./*[local-name()='MenuItem.HeaderTemplate']//*[local-name()='TextBlock' and @TextWrapping='Wrap']")) {
    throw 'FAIL R9: quick-analysis label cannot wrap.'
}
if ($code.Contains('TimeSpan.FromSeconds(p.CpuCumulativeSeconds):c') -or
    -not $code.Contains('p.CpuCumulativeAvailable') -or
    -not $code.Contains('ProcessDisplayFormatter.CpuDuration(p.CpuCumulativeSeconds)') -or
    -not $code.Contains('ProcessNetworkPresentation.DescribeEndpointOwners(p)')) {
    throw 'FAIL R9: formatted time/network are not connected to the actual details UI.'
}
Write-Host 'PASS R9: time and network presentation wired; button labels and metric cells can wrap. Structural checks only; visual Windows verification is separate.'
