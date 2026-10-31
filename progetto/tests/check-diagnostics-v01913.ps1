$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Core\StructuredDiagnostics.cs') -Raw -Encoding UTF8
$builder = Get-Content -LiteralPath (Join-Path $root 'build-dashboard.ps1') -Raw -Encoding UTF8
$launcher = Get-Content -LiteralPath (Join-Path $root 'start-processlens-all.ps1') -Raw -Encoding UTF8
$agent = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.MemoryAgent\Program.cs') -Raw -Encoding UTF8
$profiler = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Profiler\Program.cs') -Raw -Encoding UTF8
if (-not $core.Contains('SchemaVersion') -or -not $core.Contains('JsonNamingPolicy.SnakeCaseLower') -or
    -not $core.Contains('MaximumLogBytes') -or -not $core.Contains('ExceptionType')) { throw 'FAIL: catalogo eventi strutturati incompleto' }
if (-not $builder.Contains('Invoke-CheckedDotnet') -or -not $builder.Contains('ProcessLens.DiagnosticsTests')) { throw 'FAIL: diagnostica build non integrata' }
if (-not $launcher.Contains('build.jsonl')) { throw 'FAIL: rapporto della build non accessibile dal launcher' }
if (-not $agent.Contains('DiagnosticEventIds.RamMeasurement') -or -not $profiler.Contains('MEMORY_SAFETY_GATE')) { throw 'FAIL: diagnostica RAM/profiler non collegata' }
if (-not (Test-Path -LiteralPath (Join-Path $root 'diagnostics-status.ps1'))) { throw 'FAIL: consultazione diagnostica mancante' }
Write-Host 'PASS v0.19.13: catalogo eventi, build, RAM, profiler e lettore JSONL collegati.' -ForegroundColor Green
