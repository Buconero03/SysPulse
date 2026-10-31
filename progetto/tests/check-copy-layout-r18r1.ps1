$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$test = Get-Content -LiteralPath (Join-Path $root 'tests\ProcessLens.CopyUiTests\Program.cs') -Raw -Encoding UTF8
foreach ($token in @('DispatcherPriority.ContextIdle', 'DispatcherPriority.Send', 'Dispatcher.PushFrame(frame)',
 'TimeSpan.FromSeconds(5)', 'passes < 16', 'deadline.Stop()', 'deadline.Tick -= onDeadline', 'sentinel.Abort()',
 'current == previous && lines == previousLines', 'text.DesiredSize.Height > 20', 'text.LineCount >= 3',
 'narrow.Height > wide.Height && text.LineCount > wideLines', 'queued Background text is included before multiline assertions')) {
 if (-not $test.Contains($token)) { throw "FAIL R18 R1 WPF layout test: $token" }
}
# Limited lexical source check, not a C# parser. Mask non-code before checking
# case-sensitive C# member names. Keep the original fixture invariants above.
function Get-LayoutCodeViolation([string] $Source) {
    $nonCode = @'
(?s)//[^\r\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])+'
'@
    $code = [regex]::Replace($Source, $nonCode.Trim(), ' ')
    return ($code -cmatch '\.Show\s*\(' -or $code -cmatch '\bTemplate\s*=(?!=)')
}

# Reproduce the R18 R1 false positive and guard both allowed and forbidden code.
$diagnostic = '$"template={text.Template is not null}"'
if (-not ($diagnostic -match '\bTemplate\s*=')) { throw 'FAIL R18 R2 original false-positive fixture.' }
$cases = @(
    @{ Name = 'lowercase diagnostic'; Source = $diagnostic; Reject = $false },
    @{ Name = 'uppercase string'; Source = 'var s = "Template= window.Show()";'; Reject = $false },
    @{ Name = 'line comment'; Source = '// text.Template = value; window.Show();'; Reject = $false },
    @{ Name = 'block comment'; Source = '/* text.Template = value; window.Show(); */'; Reject = $false },
    @{ Name = 'verbatim string'; Source = 'var s = @"""Template = value; window.Show();""";'; Reject = $false },
    @{ Name = 'escaped quote'; Source = 'var s = "\" Template = value; window.Show();";'; Reject = $false },
    @{ Name = 'template equality'; Source = 'if (text.Template == null) return;'; Reject = $false },
    @{ Name = 'native template lookup'; Source = 'text.Template?.FindName("PART_ContentHost", text);'; Reject = $false },
    @{ Name = 'template assignment'; Source = 'text.Template = new ControlTemplate();'; Reject = $true },
    @{ Name = 'initializer assignment'; Source = 'new TextBox { Template = value };'; Reject = $true },
    @{ Name = 'show window'; Source = 'window.Show ();'; Reject = $true },
    @{ Name = 'assignment after string'; Source = 'var s = "Template="; text.Template = value;'; Reject = $true },
    @{ Name = 'show after comment'; Source = "// window.Show();`nwindow.Show();"; Reject = $true }
)
foreach ($case in $cases) {
    if ((Get-LayoutCodeViolation $case.Source) -ne $case.Reject) { throw "FAIL R18 R2 source guard: $($case.Name)" }
}
Write-Host "PASS R18 R2 source guard: $($cases.Count) regression cases; original false positive reproduced."
if (Get-LayoutCodeViolation $test) { throw 'FAIL R18 R1 must retain native theme without showing windows.' }
Write-Host 'PASS R18 R1 source: bounded native Dispatcher layout; original height, wrap and selection assertions retained.'
