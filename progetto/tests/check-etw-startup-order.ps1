$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '..\src\ProcessLens.Guardian.Service\EtwProcessMonitor.cs'
$code = [System.IO.File]::ReadAllText((Resolve-Path $source).Path)
$enable = $code.IndexOf('session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);', [System.StringComparison]::Ordinal)
$access = $code.IndexOf('var source = session.Source;', [System.StringComparison]::Ordinal)
$subscribeStart = $code.IndexOf('source.Kernel.ProcessStart +=', [System.StringComparison]::Ordinal)
$subscribeStop = $code.IndexOf('source.Kernel.ProcessStop +=', [System.StringComparison]::Ordinal)
if ($enable -lt 0 -or $access -lt 0 -or $subscribeStart -lt 0 -or $subscribeStop -lt 0) {
    throw 'Missing ETW initialization elements in EtwProcessMonitor.cs.'
}
if (-not ($enable -lt $access -and $access -lt $subscribeStart -and $subscribeStart -lt $subscribeStop)) {
    throw 'ETW order regression: EnableKernelProvider must run before access to session.Source.'
}
if ($code.IndexOf('session.Source.Kernel.', [System.StringComparison]::Ordinal) -ge 0) {
    throw 'ETW regression: direct Source access in subscription.'
}
Write-Host 'PASS ETW kernel provider precedes Source and handlers.' -ForegroundColor Green
