$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$scriptFile = Join-Path $root 'verify-dotnet-profiler.ps1'
$fixtureFile = Join-Path $root 'tests\ProcessLens.ProfilerFixture\Program.cs'
$fixtureProject = Join-Path $root 'tests\ProcessLens.ProfilerFixture\ProcessLens.ProfilerFixture.csproj'
$launcher = Get-Content -LiteralPath (Join-Path $root 'start-processlens-all.ps1') -Raw -Encoding UTF8
$build = Get-Content -LiteralPath (Join-Path $root 'build-dashboard.ps1') -Raw -Encoding UTF8
$script = Get-Content -LiteralPath $scriptFile -Raw -Encoding UTF8
$fixture = Get-Content -LiteralPath $fixtureFile -Raw -Encoding UTF8
if (-not (Test-Path -LiteralPath $fixtureProject)) { throw 'FAIL fixture: progetto mancante' }
$checks = @{
    'default senza acquisizione' = $script.Contains('if (-not $RunCapture)') -and $script.Contains('[SOLO VERIFICA]');
    'consenso scritto richiesto' = $script.Contains("-cne 'PROFILA'") -and $script.Contains('Read-Host');
    'soglia preventiva RAM' = $script.Contains('if ($usedPercent -ge 80)');
    'identita PID con ora avvio' = $script.Contains('$startTicks = $child.StartTime.ToUniversalTime().Ticks') -and $script.Contains('--start-utc-ticks');
    'nessuna installazione automatica' = -not $script.Contains('dotnet tool install');
    'nessun profiler automatico nel launcher' = -not $launcher.Contains('verify-dotnet-profiler.ps1');
    'nessun profiler automatico nella build' = -not $build.Contains('verify-dotnet-profiler.ps1');
    'target di prova con oggetti sintetici' = $fixture.Contains('KnownPayload') -and $fixture.Contains('GC.KeepAlive');
    'processo sintetico chiuso e file rimossi' = $script.Contains('$child.Kill()') -and $script.Contains('Remove-Item -LiteralPath $session');
    'nessun servizio Guardian modificato' = -not $script.Contains('Set-Service') -and -not $script.Contains('Stop-Service');
}
foreach ($item in $checks.GetEnumerator()) {
    if (-not $item.Value) { throw ('FAIL fixture: ' + $item.Key) }
    Write-Host ('PASS fixture: ' + $item.Key)
}
